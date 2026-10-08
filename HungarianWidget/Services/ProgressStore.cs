using System.Globalization;
using HungarianWidget.Models;
using Microsoft.Data.Sqlite;

namespace HungarianWidget.Services;

public sealed class ProgressStore
{
    private readonly string _connectionString;

    public ProgressStore()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagyarWidget");
        Directory.CreateDirectory(appData);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(appData, "learning.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        Initialize();
    }

    public string EnsureDailyCycle()
    {
        var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!string.Equals(GetSetting("rotation_date"), today, StringComparison.Ordinal))
        {
            SetSetting("rotation_date", today);
            SetSetting("rotation_cycle", $"{today}:0");
        }

        return GetSetting("rotation_cycle") ?? $"{today}:0";
    }

    public CardSelection? GetCurrentCard(ContentCatalog catalog)
    {
        var cycle = EnsureDailyCycle();
        var savedCycle = GetSetting("current_cycle");
        var savedId = GetSetting("current_card");
        if (savedCycle == cycle && savedId is not null && catalog.ById.TryGetValue(savedId, out var card))
            return new CardSelection(card, GetBoolSetting("current_is_review", false));
        return null;
    }

    public CardSelection? GetNextCard(IReadOnlyList<LearningCard> cards, IReadOnlySet<string> enabledCategories)
    {
        var cycle = EnsureDailyCycle();
        var seen = GetSeenIds(cycle);
        var progress = GetCardProgress();

        var eligibleCards = cards.Where(card => enabledCategories.Contains(card.Topic)).ToList();
        var freshCards = eligibleCards.Where(card => !seen.Contains(card.Id) && !progress.ContainsKey(card.Id)).ToList();
        var earlyReviewEnabled = GetSetting("early_review_cycle") == cycle;
        var dueReviews = eligibleCards
            .Where(card => !seen.Contains(card.Id)
                           && progress.TryGetValue(card.Id, out var dueAt)
                           && (dueAt <= DateTimeOffset.UtcNow || earlyReviewEnabled))
            .OrderBy(card => progress[card.Id])
            .ToList();
        var seenToday = eligibleCards.Count(card => seen.Contains(card.Id));
        var reviewSlot = dueReviews.Count > 0 && (freshCards.Count == 0 || seenToday % 3 == 2);
        LearningCard? selected = reviewSlot
            ? dueReviews[0]
            : freshCards.Count > 0
                ? freshCards[Random.Shared.Next(freshCards.Count)]
                : dueReviews.FirstOrDefault();

        if (selected is null)
            return null;

        var isReview = progress.ContainsKey(selected.Id);
        MarkCardSeen(selected, cycle, progress.ContainsKey(selected.Id));
        SaveCurrentCard(selected.Id, cycle, isReview);
        return new CardSelection(selected, isReview);
    }

    public IReadOnlyList<LearningCard> GetQuizCards(ContentCatalog catalog, IReadOnlySet<string> enabledCategories, int limit = 3)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT card_id FROM CardProgress ORDER BY last_seen_utc DESC;";
        using var reader = command.ExecuteReader();
        var cards = new List<LearningCard>();
        while (reader.Read() && cards.Count < limit)
        {
            if (catalog.ById.TryGetValue(reader.GetString(0), out var card)
                && enabledCategories.Contains(card.Topic))
                cards.Add(card);
        }
        return cards;
    }

    public void RecordQuizAnswer(string cardId, bool correct) => RecordAnswer(cardId, correct, countAsQuiz: true);

    public void RecordRecallAnswer(string cardId, bool remembered) => RecordAnswer(cardId, remembered, countAsQuiz: false);

    private void RecordAnswer(string cardId, bool correct, bool countAsQuiz)
    {
        var currentStreak = 0;
        using (var connection = OpenConnection())
        using (var query = connection.CreateCommand())
        {
            query.CommandText = "SELECT correct_streak FROM CardProgress WHERE card_id = $id;";
            query.Parameters.AddWithValue("$id", cardId);
            currentStreak = Convert.ToInt32(query.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        }

        var nextStreak = correct ? currentStreak + 1 : 0;
        var reviewAt = correct
            ? DateTimeOffset.UtcNow.AddDays(nextStreak switch { 1 => 1, 2 => 3, 3 => 7, 4 => 14, _ => 30 })
            : DateTimeOffset.UtcNow.AddHours(4);

        using var updateConnection = OpenConnection();
        using var update = updateConnection.CreateCommand();
        update.CommandText = """
            UPDATE CardProgress
            SET correct_streak = $streak,
                quiz_total = quiz_total + $quiz,
                quiz_correct = quiz_correct + $quiz_correct,
                next_review_utc = $review
            WHERE card_id = $id;
            """;
        update.Parameters.AddWithValue("$streak", nextStreak);
        update.Parameters.AddWithValue("$quiz", countAsQuiz ? 1 : 0);
        update.Parameters.AddWithValue("$quiz_correct", countAsQuiz && correct ? 1 : 0);
        update.Parameters.AddWithValue("$review", reviewAt.ToString("O", CultureInfo.InvariantCulture));
        update.Parameters.AddWithValue("$id", cardId);
        update.ExecuteNonQuery();
    }

    public string StartNewCycle(LearningCard? currentCard)
    {
        var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var cycle = $"{today}:{Guid.NewGuid():N}";
        SetSetting("rotation_date", today);
        SetSetting("rotation_cycle", cycle);
        SetSetting("early_review_cycle", cycle);
        if (currentCard is not null)
        {
            MarkCardSeen(currentCard, cycle, isReview: true);
            SaveCurrentCard(currentCard.Id, cycle, isReview: true);
        }
        else
        {
            SetSetting("current_card", "");
            SetSetting("current_cycle", cycle);
        }
        return cycle;
    }

    public bool GetBoolSetting(string name, bool defaultValue)
        => bool.TryParse(GetSetting(name), out var value) ? value : defaultValue;

    public double? GetDoubleSetting(string name)
        => double.TryParse(GetSetting(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    public void SetSetting(string name, string value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppSettings(name, value) VALUES($name, $value)
            ON CONFLICT(name) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public void SetBoolSetting(string name, bool value) => SetSetting(name, value.ToString(CultureInfo.InvariantCulture));
    public void SetDoubleSetting(string name, double value) => SetSetting(name, value.ToString(CultureInfo.InvariantCulture));

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS AppSettings(
                name TEXT PRIMARY KEY NOT NULL,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS RotationSeen(
                cycle_id TEXT NOT NULL,
                card_id TEXT NOT NULL,
                PRIMARY KEY(cycle_id, card_id)
            );
            CREATE TABLE IF NOT EXISTS CardProgress(
                card_id TEXT PRIMARY KEY NOT NULL,
                first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL,
                next_review_utc TEXT NOT NULL,
                correct_streak INTEGER NOT NULL DEFAULT 0,
                seen_count INTEGER NOT NULL DEFAULT 1,
                quiz_total INTEGER NOT NULL DEFAULT 0,
                quiz_correct INTEGER NOT NULL DEFAULT 0
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private string? GetSetting(string name)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM AppSettings WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() as string;
    }

    private HashSet<string> GetSeenIds(string cycle)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT card_id FROM RotationSeen WHERE cycle_id = $cycle;";
        command.Parameters.AddWithValue("$cycle", cycle);
        using var reader = command.ExecuteReader();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) ids.Add(reader.GetString(0));
        return ids;
    }

    private Dictionary<string, DateTimeOffset> GetCardProgress()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT card_id, next_review_utc FROM CardProgress;";
        using var reader = command.ExecuteReader();
        var progress = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            if (DateTimeOffset.TryParse(reader.GetString(1), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dueAt))
                progress[reader.GetString(0)] = dueAt;
        }
        return progress;
    }

    private void MarkCardSeen(LearningCard card, string cycle, bool isReview)
    {
        var now = DateTimeOffset.UtcNow;
        var nowText = now.ToString("O", CultureInfo.InvariantCulture);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var cycleInsert = connection.CreateCommand())
        {
            cycleInsert.Transaction = transaction;
            cycleInsert.CommandText = "INSERT OR IGNORE INTO RotationSeen(cycle_id, card_id) VALUES($cycle, $id);";
            cycleInsert.Parameters.AddWithValue("$cycle", cycle);
            cycleInsert.Parameters.AddWithValue("$id", card.Id);
            cycleInsert.ExecuteNonQuery();
        }

        using (var progressInsert = connection.CreateCommand())
        {
            progressInsert.Transaction = transaction;
            progressInsert.CommandText = """
                INSERT OR IGNORE INTO CardProgress(card_id, first_seen_utc, last_seen_utc, next_review_utc, correct_streak, seen_count)
                VALUES($id, $now, $now, $due, 0, 1);
                """;
            progressInsert.Parameters.AddWithValue("$id", card.Id);
            progressInsert.Parameters.AddWithValue("$now", nowText);
            progressInsert.Parameters.AddWithValue("$due", now.AddDays(1).ToString("O", CultureInfo.InvariantCulture));
            progressInsert.ExecuteNonQuery();
        }

        if (isReview)
        {
            using var progressUpdate = connection.CreateCommand();
            progressUpdate.Transaction = transaction;
            progressUpdate.CommandText = "UPDATE CardProgress SET last_seen_utc = $now, seen_count = seen_count + 1 WHERE card_id = $id;";
            progressUpdate.Parameters.AddWithValue("$now", nowText);
            progressUpdate.Parameters.AddWithValue("$id", card.Id);
            progressUpdate.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private void SaveCurrentCard(string cardId, string cycle, bool isReview)
    {
        SetSetting("current_card", cardId);
        SetSetting("current_cycle", cycle);
        SetBoolSetting("current_is_review", isReview);
    }
}
