namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A check of an answer.
/// </summary>
public sealed class SurveyValidationRule
{
    /// <summary>
    /// Gets or sets the kind of check.
    /// </summary>
    public SurveyValidationKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the value of the check, such as a pattern, a length or a formula.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the message shown when the check fails, or null for the default message.
    /// </summary>
    public string Message { get; set; }
}
