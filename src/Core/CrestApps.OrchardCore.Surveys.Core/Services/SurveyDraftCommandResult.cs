namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// The result of a draft command.
/// </summary>
public sealed class SurveyDraftCommandResult
{
    /// <summary>
    /// Gets or sets whether the command changed the draft.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the id of the page or question added, or <see langword="null"/>.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets why the command failed, or <see langword="null"/>.
    /// </summary>
    public string Error { get; set; }
}
