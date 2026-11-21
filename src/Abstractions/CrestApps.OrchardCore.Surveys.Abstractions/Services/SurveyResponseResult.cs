using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The result of an operation on a response.
/// </summary>
public class SurveyResponseResult
{
    /// <summary>
    /// Gets or sets the error, or <see cref="SurveyErrorCode.None"/>.
    /// </summary>
    public SurveyErrorCode Error { get; set; }

    /// <summary>
    /// Gets or sets the response, or <see langword="null"/>.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public bool Succeeded => Error == SurveyErrorCode.None;
}
