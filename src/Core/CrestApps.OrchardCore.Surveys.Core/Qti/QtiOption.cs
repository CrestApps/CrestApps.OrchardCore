namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// An option of a choice question, or an item of an ordering question.
/// </summary>
public sealed class QtiOption
{
    /// <summary>Gets or sets the identifier of the choice in the item.</summary>
    public string Identifier { get; set; }

    /// <summary>Gets or sets the text.</summary>
    public string Text { get; set; }

    /// <summary>Gets or sets whether the option is right.</summary>
    public bool Correct { get; set; }

    /// <summary>Gets or sets the feedback shown when the option was chosen, as plain text.</summary>
    public string Feedback { get; set; }
}
