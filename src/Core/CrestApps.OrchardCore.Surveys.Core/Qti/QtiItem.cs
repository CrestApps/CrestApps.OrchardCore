namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// One assessment item: the question it became, or why it was refused.
/// </summary>
public sealed class QtiItem
{
    /// <summary>Gets or sets the identifier of the item.</summary>
    public string Identifier { get; set; }

    /// <summary>Gets or sets the title of the item.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the path of the item file in the package.</summary>
    public string Path { get; set; }

    /// <summary>Gets or sets the QTI version: <c>2.1</c> (2.x) or <c>3.0</c>.</summary>
    public string Version { get; set; }

    /// <summary>Gets or sets the question, or <see langword="null"/> when the item was refused.</summary>
    public QtiQuestion Question { get; set; }

    /// <summary>Gets or sets why the item was refused, one of <see cref="QtiRefusals"/>, or <see langword="null"/>.</summary>
    public string Refusal { get; set; }

    /// <summary>Gets or sets what the refusal names.</summary>
    public string RefusalDetail { get; set; }

    /// <summary>Gets or sets the media files the prompt and feedback refer to.</summary>
    public IReadOnlyList<QtiMediaFile> Media { get; set; } = [];

    /// <summary>Gets whether the item became a question.</summary>
    public bool Imported => Refusal is null && Question is not null;
}
