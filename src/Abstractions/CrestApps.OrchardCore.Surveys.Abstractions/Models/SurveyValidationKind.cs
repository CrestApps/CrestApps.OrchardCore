namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A kind of answer check.
/// </summary>
public enum SurveyValidationKind
{
    /// <summary>
    /// The answer matches a regular expression.
    /// </summary>
    Regex,

    /// <summary>
    /// The answer has at least this many characters.
    /// </summary>
    MinLength,

    /// <summary>
    /// The answer has at most this many characters.
    /// </summary>
    MaxLength,

    /// <summary>
    /// The number is at least this value.
    /// </summary>
    Min,

    /// <summary>
    /// The number is at most this value.
    /// </summary>
    Max,

    /// <summary>
    /// A formula holds.
    /// </summary>
    Formula,

    /// <summary>
    /// The answer is an e-mail address.
    /// </summary>
    Email,

    /// <summary>
    /// The answer is a URL.
    /// </summary>
    Url,
}
