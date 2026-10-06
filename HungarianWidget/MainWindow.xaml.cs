using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using HungarianWidget.Models;
using HungarianWidget.Services;
using Forms = System.Windows.Forms;

namespace HungarianWidget;

public partial class MainWindow : Window
{
    private const double DefaultCompactWidth = 470;
    private const double DefaultCompactHeight = 485;
    private const double MinimumWidgetWidth = 400;
    private const double MinimumWidgetHeight = 450;
    private const double DefaultQuizExpansionHeight = 250;

    private readonly ContentCatalog _catalog;
    private readonly ProgressStore _progress;
    private readonly AudioPlayer _audioPlayer = new();
    private readonly List<QuizQuestion> _quizQuestions = [];
    private readonly HashSet<string> _enabledCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MenuItem> _categoryMenuItems = new(StringComparer.OrdinalIgnoreCase);
    private MenuItem? _categoriesMenuItem;
    private Forms.NotifyIcon? _trayIcon;
    private CardSelection? _currentCard;
    private int _quizIndex;
    private int _quizScore;
    private bool _answerSubmitted;
    private bool _allowClose;
    private bool _quizExpanded;
    private bool _lightTheme;
    private double _compactWidth = DefaultCompactWidth;
    private double _compactHeight = DefaultCompactHeight;
    private double _quizExtraHeight = DefaultQuizExpansionHeight;
    private double? _compactTop;

    public MainWindow(ContentCatalog catalog, ProgressStore progress)
    {
        InitializeComponent();
        _catalog = catalog;
        _progress = progress;
        var workArea = SystemParameters.WorkArea;
        _compactWidth = Math.Clamp(_progress.GetDoubleSetting("window_width") ?? DefaultCompactWidth,
            MinimumWidgetWidth, Math.Max(MinimumWidgetWidth, workArea.Width - 16));
        _compactHeight = Math.Clamp(_progress.GetDoubleSetting("window_height") ?? DefaultCompactHeight,
            MinimumWidgetHeight, Math.Max(MinimumWidgetHeight, workArea.Height - 16));
        Width = _compactWidth;
        Height = _compactHeight;
        LoadEnabledCategories();

        Topmost = _progress.GetBoolSetting("always_on_top", defaultValue: true);
        ApplyTheme(_progress.GetBoolSetting("light_theme", defaultValue: false));
        _audioPlayer.StatusChanged += status => Dispatcher.BeginInvoke(() => AudioStatusText.Text = status);

        var saved = _progress.GetCurrentCard(_catalog);
        if (saved is not null && _enabledCategories.Contains(saved.Card.Topic))
            ShowCard(saved);
        else
            ShowNextCardOrEmpty();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestorePosition();
        CreateTrayIcon();
    }

    private void RerollButton_Click(object sender, RoutedEventArgs e)
    {
        CloseQuiz();
        ShowNextCardOrEmpty();
    }

    private void ListenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentCard is null) return;
        _audioPlayer.Play(_currentCard.Card);
    }

    private void ListenSentenceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentCard is null) return;
        _audioPlayer.PlayExample(_currentCard.Card);
    }

    private void QuizButton_Click(object sender, RoutedEventArgs e)
    {
        if (_quizExpanded)
        {
            CloseQuiz();
            return;
        }

        StartQuiz();
    }

    private void QuizAgainButton_Click(object sender, RoutedEventArgs e) => StartQuiz();

    private void QuizNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_answerSubmitted) return;
        if (_quizIndex + 1 < _quizQuestions.Count)
        {
            _quizIndex++;
            ShowQuizQuestion();
            return;
        }

        QuizQuestionPanel.Visibility = Visibility.Collapsed;
        QuizResultsPanel.Visibility = Visibility.Visible;
        QuizCounterText.Text = "DONE";
        QuizResultText.Text = $"{_quizScore} of {_quizQuestions.Count} correct";
        QuizResultHintText.Text = _quizScore == _quizQuestions.Count
            ? "Great recall. Your next cards are ready when you are."
            : "The missed cards are scheduled to come back for review.";
    }

    private void NewCycleButton_Click(object sender, RoutedEventArgs e)
    {
        CloseQuiz();
        _progress.StartNewCycle(_currentCard?.Card);
        ShowNextCardOrEmpty();
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var topmost = new MenuItem { Header = "Always on top", IsCheckable = true, IsChecked = Topmost };
        topmost.Click += (_, _) => SetTopmost(topmost.IsChecked);
        var startup = new MenuItem { Header = "Start with Windows", IsCheckable = true, IsChecked = StartupManager.IsEnabled };
        startup.Click += (_, _) => SetStartup(startup.IsChecked);
        var lightTheme = new MenuItem { Header = "Light theme", IsCheckable = true, IsChecked = _lightTheme };
        lightTheme.Click += (_, _) => ApplyTheme(lightTheme.IsChecked);
        var categories = new MenuItem
        {
            Header = $"Categories ({_enabledCategories.Count}/{_catalog.Categories.Count})",
            StaysOpenOnClick = true
        };
        _categoriesMenuItem = categories;
        _categoryMenuItems.Clear();
        var enableAll = new MenuItem { Header = "Select all" };
        enableAll.Click += (_, _) => SetAllCategories(true);
        var disableAll = new MenuItem { Header = "Clear all" };
        disableAll.Click += (_, _) => SetAllCategories(false);
        categories.Items.Add(enableAll);
        categories.Items.Add(disableAll);
        categories.Items.Add(new Separator());
        foreach (var topic in _catalog.Categories)
        {
            var category = new MenuItem
            {
                Header = topic,
                IsCheckable = true,
                IsChecked = _enabledCategories.Contains(topic),
                StaysOpenOnClick = true
            };
            category.Click += (_, _) => SetCategoryEnabled(topic, category.IsChecked);
            categories.Items.Add(category);
            _categoryMenuItems[topic] = category;
        }
        var hide = new MenuItem { Header = "Hide to system tray" };
        hide.Click += (_, _) => HideToTray();
        var exit = new MenuItem { Header = "Exit Magyar" };
        exit.Click += (_, _) => ExitApplication();
        menu.Items.Add(topmost);
        menu.Items.Add(startup);
        menu.Items.Add(lightTheme);
        menu.Items.Add(categories);
        menu.Items.Add(new Separator());
        menu.Items.Add(hide);
        menu.Items.Add(exit);
        menu.PlacementTarget = MenuButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { }
            SavePosition();
        }
    }

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        var maxWidth = Math.Max(MinimumWidgetWidth, workArea.Right - Left - 8);
        var maxHeight = Math.Max(MinimumWidgetHeight, workArea.Bottom - Top - 8);
        Width = Math.Clamp(Width + e.HorizontalChange, MinimumWidgetWidth, maxWidth);
        Height = Math.Clamp(Height + e.VerticalChange, MinimumWidgetHeight, maxHeight);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded) return;

        _compactWidth = Width;
        _compactHeight = _quizExpanded
            ? Math.Max(MinimumWidgetHeight, Height - _quizExtraHeight)
            : Height;
        SaveWindowSize();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        SavePosition();
        _audioPlayer.Dispose();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }

    private void ShowCard(CardSelection? selection)
    {
        _currentCard = selection;
        if (selection is null)
        {
            ShowRotationComplete();
            return;
        }

        CardContent.Visibility = Visibility.Visible;
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        QuizButton.IsEnabled = true;
        NewCycleButton.Visibility = Visibility.Visible;
        ListenButton.IsEnabled = true;
        ListenSentenceButton.IsEnabled = true;
        BadgeText.Text = selection.IsReview
            ? "SPACED REVIEW"
            : "NEW WORD";
        ApplyBadgeTheme(selection.IsReview);
        TopicText.Text = selection.Card.Topic.ToUpperInvariant();
        HungarianText.Text = selection.Card.Hungarian;
        EnglishText.Text = selection.Card.English;
        ExampleHungarianText.Text = selection.Card.ExampleHungarian;
        ExampleEnglishText.Text = selection.Card.ExampleEnglish;
        AudioStatusText.Text = selection.IsReview ? "Due for a quick review" : "Tap to hear Hungarian";
    }

    private void ShowRotationComplete()
    {
        CardContent.Visibility = Visibility.Collapsed;
        EmptyStatePanel.Visibility = Visibility.Visible;
        QuizButton.IsEnabled = true;
        NewCycleButton.Visibility = Visibility.Visible;
        ListenButton.IsEnabled = false;
        ListenSentenceButton.IsEnabled = false;
        AudioStatusText.Text = "No repeat cards in this rotation";
        EmptyTitleText.Text = "You've seen this rotation.";
        EmptyDescriptionText.Text = "Start another rotation for more cards. Due cards return for review later.";
    }

    private void ShowNoCategoriesSelected()
    {
        CardContent.Visibility = Visibility.Collapsed;
        EmptyStatePanel.Visibility = Visibility.Visible;
        QuizButton.IsEnabled = false;
        NewCycleButton.Visibility = Visibility.Collapsed;
        ListenButton.IsEnabled = false;
        ListenSentenceButton.IsEnabled = false;
        AudioStatusText.Text = "Choose a category in the ⋯ menu";
        EmptyTitleText.Text = "No categories are selected.";
        EmptyDescriptionText.Text = "Open ⋯, then Categories, and turn on the subjects you want to study.";
    }

    private void LoadEnabledCategories()
    {
        foreach (var topic in _catalog.Categories)
        {
            if (_progress.GetBoolSetting(CategorySettingName(topic), defaultValue: true))
                _enabledCategories.Add(topic);
        }
    }

    private void SetCategoryEnabled(string topic, bool enabled)
    {
        if (enabled) _enabledCategories.Add(topic);
        else _enabledCategories.Remove(topic);

        _progress.SetBoolSetting(CategorySettingName(topic), enabled);
        if (_categoryMenuItems.TryGetValue(topic, out var categoryItem))
            categoryItem.IsChecked = enabled;
        UpdateCategoryMenuHeader();
        RefreshForCategoryChange();
    }

    private void SetAllCategories(bool enabled)
    {
        _enabledCategories.Clear();
        if (enabled)
            foreach (var topic in _catalog.Categories)
                _enabledCategories.Add(topic);

        foreach (var topic in _catalog.Categories)
            _progress.SetBoolSetting(CategorySettingName(topic), enabled);
        foreach (var categoryItem in _categoryMenuItems.Values)
            categoryItem.IsChecked = enabled;
        UpdateCategoryMenuHeader();
        RefreshForCategoryChange();
    }

    private void UpdateCategoryMenuHeader()
    {
        if (_categoriesMenuItem is not null)
            _categoriesMenuItem.Header = $"Categories ({_enabledCategories.Count}/{_catalog.Categories.Count})";
    }

    private void RefreshForCategoryChange()
    {
        if (_quizExpanded) CloseQuiz();
        if (_enabledCategories.Count == 0)
        {
            ShowNoCategoriesSelected();
            return;
        }

        if (_currentCard is not null
            && _enabledCategories.Contains(_currentCard.Card.Topic)
            && CardContent.Visibility == Visibility.Visible)
            return;

        ShowNextCardOrEmpty();
    }

    private void ShowNextCardOrEmpty()
    {
        if (_enabledCategories.Count == 0)
        {
            ShowNoCategoriesSelected();
            return;
        }

        var selection = _progress.GetNextCard(_catalog.Cards, _enabledCategories);
        if (selection is null) ShowRotationComplete();
        else ShowCard(selection);
    }

    private static string CategorySettingName(string topic) => $"category:{topic}";

    private void StartQuiz()
    {
        var seenCards = _progress.GetQuizCards(_catalog, _enabledCategories, 3)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        if (seenCards.Count == 0)
        {
            QuizQuestionPanel.Visibility = Visibility.Collapsed;
            QuizResultsPanel.Visibility = Visibility.Visible;
            QuizCounterText.Text = "READY";
            QuizResultText.Text = "See a card first.";
            QuizResultHintText.Text = "Reroll through a few new words, then come back for a quiz.";
            ExpandQuiz();
            return;
        }

        _quizQuestions.Clear();
        for (var i = 0; i < seenCards.Count; i++)
            _quizQuestions.Add(CreateQuestion(seenCards[i], askForEnglish: i % 2 == 0));

        _quizIndex = 0;
        _quizScore = 0;
        QuizQuestionPanel.Visibility = Visibility.Visible;
        QuizResultsPanel.Visibility = Visibility.Collapsed;
        ExpandQuiz();
        ShowQuizQuestion();
    }

    private QuizQuestion CreateQuestion(LearningCard card, bool askForEnglish)
    {
        var correctAnswer = askForEnglish ? card.English : card.Hungarian;
        var distractors = _catalog.Cards
            .Where(candidate => _enabledCategories.Contains(candidate.Topic)
                                && !candidate.Id.Equals(card.Id, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => askForEnglish ? candidate.English : candidate.Hungarian)
            .Where(answer => !answer.Equals(correctAnswer, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(_ => Random.Shared.Next())
            .Take(3)
            .ToList();
        var choices = distractors.Append(correctAnswer).OrderBy(_ => Random.Shared.Next()).ToList();
        return new QuizQuestion(card, askForEnglish, correctAnswer, choices);
    }

    private void ShowQuizQuestion()
    {
        if (_quizIndex >= _quizQuestions.Count) return;
        var question = _quizQuestions[_quizIndex];
        _answerSubmitted = false;
        QuizCounterText.Text = $"{_quizIndex + 1} / {_quizQuestions.Count}";
        QuizPromptText.Text = question.AskForEnglish ? "What does this mean in English?" : "How do you say this in Hungarian?";
        QuizQuestionText.Text = question.AskForEnglish ? question.Card.Hungarian : question.Card.English;
        QuizFeedbackText.Text = "";
        QuizNextButton.IsEnabled = false;
        QuizNextButton.Content = _quizIndex + 1 == _quizQuestions.Count ? "See your score  →" : "Next question  →";
        QuizOptionsPanel.Children.Clear();

        foreach (var choice in question.Choices)
        {
            var option = new Button
            {
                Content = choice,
                Tag = choice,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 0, 6),
                MinHeight = 34,
                Background = new SolidColorBrush(Color.FromRgb(43, 54, 80)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 73, 101)),
                Foreground = new SolidColorBrush(Color.FromRgb(245, 246, 252))
            };
            if (_lightTheme)
            {
                option.Background = Brush("#F3F2F9");
                option.BorderBrush = Brush("#D9D8E6");
                option.Foreground = Brush("#24243A");
            }
            option.Click += QuizOption_Click;
            QuizOptionsPanel.Children.Add(option);
        }
    }

    private void QuizOption_Click(object sender, RoutedEventArgs e)
    {
        if (_answerSubmitted || sender is not Button selected || _quizIndex >= _quizQuestions.Count) return;
        _answerSubmitted = true;
        var question = _quizQuestions[_quizIndex];
        var selectedAnswer = selected.Tag?.ToString() ?? "";
        var correct = selectedAnswer.Equals(question.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
        if (correct) _quizScore++;

        _progress.RecordQuizAnswer(question.Card.Id, correct);
        foreach (var item in QuizOptionsPanel.Children.OfType<Button>())
        {
            item.IsEnabled = false;
            if ((item.Tag?.ToString() ?? "").Equals(question.CorrectAnswer, StringComparison.OrdinalIgnoreCase))
            {
                item.Background = new SolidColorBrush(Color.FromRgb(37, 87, 73));
                item.BorderBrush = new SolidColorBrush(Color.FromRgb(75, 151, 121));
            }
            else if (ReferenceEquals(item, selected))
            {
                item.Background = new SolidColorBrush(Color.FromRgb(91, 51, 64));
                item.BorderBrush = new SolidColorBrush(Color.FromRgb(167, 92, 112));
            }
        }

        QuizFeedbackText.Text = correct ? "Correct!" : $"Not quite — the answer is {question.CorrectAnswer}.";
        QuizFeedbackText.Foreground = correct
            ? new SolidColorBrush(Color.FromRgb(157, 224, 197))
            : new SolidColorBrush(Color.FromRgb(244, 176, 180));
        QuizNextButton.IsEnabled = true;
    }

    private void CloseQuiz()
    {
        _quizExpanded = false;
        QuizPanel.Visibility = Visibility.Collapsed;
        QuizButton.Content = "Quiz";
        Width = _compactWidth;
        Height = _compactHeight;
        if (_compactTop.HasValue)
        {
            Top = Math.Min(_compactTop.Value, SystemParameters.WorkArea.Bottom - Height - 8);
            _compactTop = null;
        }
    }

    private void ExpandQuiz()
    {
        if (!_quizExpanded)
        {
            _compactTop = Top;
            _compactWidth = Width;
            _compactHeight = Height;
        }
        _quizExpanded = true;
        QuizPanel.Visibility = Visibility.Visible;
        QuizButton.Content = "Close quiz";
        var workArea = SystemParameters.WorkArea;
        var expandedHeight = Math.Min(_compactHeight + DefaultQuizExpansionHeight, workArea.Height - 16);
        _quizExtraHeight = Math.Max(0, expandedHeight - _compactHeight);
        Height = expandedHeight;
        if (Top + Height > workArea.Bottom - 8)
            Top = Math.Max(workArea.Top + 8, workArea.Bottom - Height - 8);
    }

    private void SetTopmost(bool enabled)
    {
        Topmost = enabled;
        _progress.SetBoolSetting("always_on_top", enabled);
    }

    private void ApplyTheme(bool light)
    {
        _lightTheme = light;
        _progress.SetBoolSetting("light_theme", light);

        Application.Current.Resources["ButtonForegroundBrush"] = Brush(light ? "#25263D" : "#F5F6FC");
        Application.Current.Resources["ButtonBackgroundBrush"] = Brush(light ? "#F3F2F9" : "#2B3650");
        Application.Current.Resources["ButtonBorderBrush"] = Brush(light ? "#D9D8E6" : "#3C4965");

        WidgetBorder.Background = Brush(light ? "#FAF9FE" : "#151C2C");
        WidgetBorder.BorderBrush = Brush(light ? "#D9D9E7" : "#53617D");
        BrandText.Foreground = Brush(light ? "#26263B" : "#F6F5FF");
        HungarianText.Foreground = Brush(light ? "#1F2035" : "#FBFAFF");
        EnglishText.Foreground = Brush(light ? "#555A70" : "#B7C1D4");
        TopicText.Foreground = Brush(light ? "#777B8D" : "#97A2BA");
        EmptyTitleText.Foreground = Brush(light ? "#25263A" : "#F6F5FF");
        EmptyDescriptionText.Foreground = Brush(light ? "#686D81" : "#AAB4C8");
        ExampleDivider.Background = Brush(light ? "#DDDEEA" : "#39445A");
        AudioStatusText.Foreground = Brush(light ? "#73778A" : "#8E9BB4");
        ListenButton.Background = Brush(light ? "#EAE9F3" : "#2D3852");
        ListenButton.BorderBrush = Brush(light ? "#D2D2E2" : "#54617A");
        ListenSentenceButton.Background = Brush(light ? "#EAE9F3" : "#2D3852");
        ListenSentenceButton.BorderBrush = Brush(light ? "#D2D2E2" : "#54617A");
        QuizPanel.Background = Brush(light ? "#F1F1F8" : "#202A40");
        QuizPanel.BorderBrush = Brush(light ? "#D9DCEC" : "#394660");
        QuizCounterText.Foreground = Brush(light ? "#777B8D" : "#99A6BF");
        QuizPromptText.Foreground = Brush(light ? "#686D81" : "#AAB4C8");
        QuizQuestionText.Foreground = Brush(light ? "#222339" : "#F7F6FF");
        QuizFeedbackText.Foreground = Brush(light ? "#52576C" : "#D5D7E2");
        QuizResultText.Foreground = Brush(light ? "#222339" : "#F7F6FF");
        QuizResultHintText.Foreground = Brush(light ? "#686D81" : "#AAB4C8");
        if (_currentCard is not null) ApplyBadgeTheme(_currentCard.IsReview);
    }

    private void ApplyBadgeTheme(bool review)
    {
        if (_lightTheme)
        {
            BadgeBorder.Background = Brush(review ? "#DDEEE9" : "#E8E4FA");
            BadgeText.Foreground = Brush(review ? "#316356" : "#514489");
        }
        else
        {
            BadgeBorder.Background = Brush(review ? "#2C424A" : "#2F3357");
            BadgeText.Foreground = Brush(review ? "#BAE7DC" : "#D9D3FF");
        }
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);

    private void SetStartup(bool enabled)
    {
        try
        {
            StartupManager.SetEnabled(enabled);
            _progress.SetBoolSetting("start_with_windows", enabled);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The startup setting could not be changed.\n\n{ex.Message}", "Magyar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RestorePosition()
    {
        var workArea = SystemParameters.WorkArea;
        var left = _progress.GetDoubleSetting("window_left");
        var top = _progress.GetDoubleSetting("window_top");
        if (!left.HasValue || !top.HasValue || left.Value < workArea.Left - Width || left.Value > workArea.Right
            || top.Value < workArea.Top - Height || top.Value > workArea.Bottom)
        {
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 28;
            return;
        }

        Left = Math.Clamp(left.Value, workArea.Left + 8, workArea.Right - Width - 8);
        Top = Math.Clamp(top.Value, workArea.Top + 8, workArea.Bottom - Height - 8);
    }

    private void SavePosition()
    {
        if (!IsLoaded || double.IsNaN(Left) || double.IsNaN(Top)) return;
        _progress.SetDoubleSetting("window_left", Left);
        _progress.SetDoubleSetting("window_top", _quizExpanded && _compactTop.HasValue ? _compactTop.Value : Top);
        SaveWindowSize();
    }

    private void SaveWindowSize()
    {
        _progress.SetDoubleSetting("window_width", _compactWidth);
        _progress.SetDoubleSetting("window_height", _compactHeight);
    }

    private void CreateTrayIcon()
    {
        if (_trayIcon is not null) return;
        var menu = new Forms.ContextMenuStrip();
        var showItem = new Forms.ToolStripMenuItem("Show Magyar");
        showItem.Click += (_, _) => ShowWidget();
        var topItem = new Forms.ToolStripMenuItem("Always on top") { CheckOnClick = true, Checked = Topmost };
        topItem.CheckedChanged += (_, _) => SetTopmost(topItem.Checked);
        var startupItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupManager.IsEnabled
        };
        startupItem.CheckedChanged += (_, _) => SetStartup(startupItem.Checked);
        var lightThemeItem = new Forms.ToolStripMenuItem("Light theme")
        {
            CheckOnClick = true,
            Checked = _lightTheme
        };
        lightThemeItem.CheckedChanged += (_, _) => ApplyTheme(lightThemeItem.Checked);
        var exitItem = new Forms.ToolStripMenuItem("Exit Magyar");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(showItem);
        menu.Items.Add(topItem);
        menu.Items.Add(startupItem);
        menu.Items.Add(lightThemeItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Text = "Magyar — Hungarian learning card",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowWidget();
    }

    private void ShowWidget()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        SavePosition();
        Hide();
    }

    private void ExitApplication()
    {
        _allowClose = true;
        Close();
        Application.Current.Shutdown();
    }

    private sealed record QuizQuestion(LearningCard Card, bool AskForEnglish, string CorrectAnswer, IReadOnlyList<string> Choices);
}
