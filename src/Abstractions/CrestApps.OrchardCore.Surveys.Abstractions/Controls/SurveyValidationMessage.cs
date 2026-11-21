namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// One problem found in a survey definition.
/// </summary>
public sealed class SurveyValidationMessage
{
    /// <summary>
    /// Gets or sets whether it blocks publishing.
    /// </summary>
    public bool IsError { get; set; }

    /// <summary>
    /// Gets or sets a stable code, such as duplicate-name.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the localized message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the id of the page concerned, or <see langword="null"/>.
    /// </summary>
    public string PageId { get; set; }

    /// <summary>
    /// Gets or sets the id of the question concerned, or <see langword="null"/>.
    /// </summary>
    public string QuestionId { get; set; }
}
