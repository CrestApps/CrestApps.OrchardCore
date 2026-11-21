using CrestApps.OrchardCore.Surveys.Controls;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What publishing a draft would do: the problems found and the changes from the published version.
/// </summary>
public sealed class SurveyPublishPreview
{
    /// <summary>
    /// Gets or sets the problems found.
    /// </summary>
    public IReadOnlyList<SurveyValidationMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the changes from the published version.
    /// </summary>
    public SurveyDefinitionDiff Diff { get; set; } = new();

    /// <summary>
    /// Gets whether the draft can be published.
    /// </summary>
    public bool CanPublish => !Messages.Any(message => message.IsError);
}
