namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// The agent needs more information before it can answer.
/// </summary>
public sealed class NLWebElicitationEvent : NLWebEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebElicitationEvent"/> class.
    /// </summary>
    /// <param name="text">The text that introduces the questions.</param>
    /// <param name="questions">The questions.</param>
    public NLWebElicitationEvent(
        string text,
        IReadOnlyList<NLWebQuestion> questions)
    {
        Text = text;
        Questions = questions ?? [];
    }

    /// <summary>
    /// Gets the text that introduces the questions.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the questions.
    /// </summary>
    public IReadOnlyList<NLWebQuestion> Questions { get; }
}
