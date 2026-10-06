namespace HungarianWidget.Models;

public sealed class WordNote
{
    public string CardId { get; init; } = "";
    public string PartOfSpeech { get; init; } = "";
    public string Footer { get; init; } = "";
    public WordForm[] Forms { get; init; } = [];
    public WordRelation[] Synonyms { get; init; } = [];
    public WordRelation[] RelatedWords { get; init; } = [];
}

public sealed class WordForm
{
    public string Hungarian { get; init; } = "";
    public string Label { get; init; } = "";
    public string English { get; init; } = "";
}

public sealed class WordRelation
{
    public string Hungarian { get; init; } = "";
    public string English { get; init; } = "";
    public string Note { get; init; } = "";
}
