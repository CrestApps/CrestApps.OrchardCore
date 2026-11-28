using CrestApps.OrchardCore.Surveys.Core.Controls.ChoiceBlanks;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of choice blanks questions in the designer.
/// </summary>
public sealed class ChoiceBlanksSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<ChoiceBlanksSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChoiceBlanksSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ChoiceBlanksSettingsDisplayDriver(IStringLocalizer<ChoiceBlanksSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.ChoiceBlanks;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Choose the Missing Words Settings"];

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
