using System.Reflection;
using System.Text.Json;
using HungarianWidget.Models;

namespace HungarianWidget.Services;

public sealed class ContentCatalog
{
    private ContentCatalog(IReadOnlyList<LearningCard> cards)
    {
        Cards = cards;
        ById = cards.ToDictionary(card => card.Id, StringComparer.OrdinalIgnoreCase);
        Categories = cards.Select(card => card.Topic)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(topic => topic, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<LearningCard> Cards { get; }
    public IReadOnlyDictionary<string, LearningCard> ById { get; }
    public IReadOnlyList<string> Categories { get; }

    public static ContentCatalog Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var initialCards = ReadPack(assembly, "Content.cards.json").Cards;
        var expandedCards = ReadPack(assembly, "Content.expanded-cards.json").Cards;
        var allCards = initialCards.Concat(expandedCards).ToList();
        if (allCards.Count == 0)
            throw new InvalidOperationException("The Hungarian content pack is empty or invalid.");
        if (allCards.Any(card => string.IsNullOrWhiteSpace(card.Id)
                                 || string.IsNullOrWhiteSpace(card.Hungarian)
                                 || string.IsNullOrWhiteSpace(card.English)))
            throw new InvalidOperationException("A card is missing its ID or translation.");
        if (allCards.Select(card => card.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != allCards.Count)
            throw new InvalidOperationException("The Hungarian content pack contains duplicate card IDs.");

        var sentences = allCards
            .Where(card => card.Kind.Equals("sentence", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var vocabulary = allCards
            .Where(card => card.Kind.Equals("word", StringComparison.OrdinalIgnoreCase))
            .Select(word =>
            {
                if (!string.IsNullOrWhiteSpace(word.ExampleHungarian)
                    && !string.IsNullOrWhiteSpace(word.ExampleEnglish)
                    && !string.IsNullOrWhiteSpace(word.ExampleAudioId))
                    return MakeWordCard(word, word.ExampleHungarian, word.ExampleEnglish, word.ExampleAudioId);

                var example = sentences.FirstOrDefault(sentence =>
                    sentence.Id.Equals($"ex-{word.Id}", StringComparison.OrdinalIgnoreCase))
                    ?? sentences.FirstOrDefault(sentence =>
                    sentence.TargetWordIds.Contains(word.Id, StringComparer.OrdinalIgnoreCase));
                if (example is null)
                    throw new InvalidOperationException($"The word '{word.Hungarian}' has no example sentence.");

                return MakeWordCard(word, example.Hungarian, example.English, example.Id);
            })
            .ToList();

        return new ContentCatalog(vocabulary);
    }

    private static ContentPack ReadPack(Assembly assembly, string suffix)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new InvalidOperationException($"The content pack '{suffix}' is missing.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The content pack '{suffix}' could not be opened.");
        return JsonSerializer.Deserialize<ContentPack>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"The content pack '{suffix}' is invalid.");
    }

    private static LearningCard MakeWordCard(LearningCard word, string exampleHungarian, string exampleEnglish, string exampleAudioId)
        => new()
        {
            Id = word.Id,
            Hungarian = word.Hungarian,
            English = word.English,
            Kind = "word",
            Topic = NormalizeTopic(word),
            ExampleHungarian = exampleHungarian,
            ExampleEnglish = exampleEnglish,
            ExampleAudioId = exampleAudioId
        };

    private static string NormalizeTopic(LearningCard card)
    {
        if (card.Id is "w-haz" or "w-lakas" or "w-szoba")
            return "Home & everyday objects";

        return card.Topic.ToLowerInvariant() switch
        {
            "greetings" or "basics" or "questions" => "Street talk",
            "shopping" => "Shops & services",
            "food & drink" => "Food & drink",
            "places" or "directions" => "City & directions",
            "daily life" => "Work & study",
            "people" => "People & relationships",
            "travel" => "Travel & transport",
            "time & place" => "Time & weather",
            "descriptions" => "Descriptions",
            "how you feel" => "Health & body",
            "numbers" => "Numbers & money",
            "verbs" => "Common verbs",
            "things" => "Home & everyday objects",
            "emergencies" => "Emergencies",
            _ => card.Topic
        };
    }

    private sealed class ContentPack
    {
        public int Version { get; init; }
        public List<LearningCard> Cards { get; init; } = [];
    }
}
