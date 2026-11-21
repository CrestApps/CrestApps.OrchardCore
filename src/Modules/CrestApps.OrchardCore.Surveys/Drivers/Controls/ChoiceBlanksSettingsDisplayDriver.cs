using CrestApps.OrchardCore.Surveys.Core.Controls.ChoiceBlanks;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of choice blanks questions in the designer.
/// </summary>
public sealed class ChoiceBlanksSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<ChoiceBlanksSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.ChoiceBlanks;

    /// <inheritdoc />
    protected override Task UpdateSettingsAsync(SurveyQuestion question, ChoiceBlanksSettings settings, UpdateEditorContext context)
    {
        settings.Distractors = (settings.Distractors ?? [])
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Task.CompletedTask;
    }
}
