using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The result of publishing a draft.
/// </summary>
public sealed class SurveyPublishResult
{
    /// <summary>
    /// Gets or sets the error, or <see cref="SurveyErrorCode.None"/>.
    /// </summary>
    public SurveyErrorCode Error { get; set; }

    /// <summary>
    /// Gets or sets the new version, or <see langword="null"/>.
    /// </summary>
    public SurveyVersion Version { get; set; }

    /// <summary>
    /// Gets or sets the problems found.
    /// </summary>
    public IReadOnlyList<SurveyValidationMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets whether the draft was published.
    /// </summary>
    public bool Succeeded => Error == SurveyErrorCode.None && Version is not null;
}
