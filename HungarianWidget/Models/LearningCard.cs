namespace HungarianWidget.Models;

public sealed class LearningCard
{
    public string Id { get; init; } = "";
    public string Hungarian { get; init; } = "";
    public string English { get; init; } = "";
    public string Kind { get; init; } = "word";
    public string Topic { get; init; } = "Basics";
    public string[] TargetWordIds { get; init; } = [];
    public string ExampleHungarian { get; init; } = "";
    public string ExampleEnglish { get; init; } = "";
    public string ExampleAudioId { get; init; } = "";
}

public sealed record CardSelection(LearningCard Card, bool IsReview);
