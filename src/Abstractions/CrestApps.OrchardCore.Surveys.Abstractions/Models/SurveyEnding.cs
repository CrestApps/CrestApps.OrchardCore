namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How a response ends: the message or redirect shown, and whether it counts as completed.
/// </summary>
public sealed class SurveyEnding
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the variable code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the message, Liquid with sanitized HTML.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets a local or allowed URL the respondent is sent to, or null.
    /// </summary>
    public string RedirectUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the response counts as completed or as screened out.
    /// </summary>
    public EndingOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets when this ending is chosen after the last page.
    /// </summary>
    public SurveyVisibility Condition { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this ending is used when no other ending applies.
    /// </summary>
    public bool IsDefault { get; set; }
}
