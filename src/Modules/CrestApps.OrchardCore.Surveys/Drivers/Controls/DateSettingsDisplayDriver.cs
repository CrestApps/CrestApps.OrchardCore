using CrestApps.OrchardCore.Surveys.Core.Controls.Date;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Date questions in the designer.
/// </summary>
public sealed class DateSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<DateSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DateSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public DateSettingsDisplayDriver(IStringLocalizer<DateSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Date;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Date and Time Settings"];

    /// <inheritdoc />
    protected override Task UpdateSettingsAsync(SurveyQuestion question, DateSettings settings, UpdateEditorContext context)
    {
        // Earliest and Latest keep only the part the mode asks for, whichever editor posted them: a time posted on its own
        // is bound to today's date, and a date and time posted before the mode changed carries both.
        settings.Min = settings.Min is { } min ? DateControlHandler.Normalize(min, settings.Mode) : null;
        settings.Max = settings.Max is { } max ? DateControlHandler.Normalize(max, settings.Mode) : null;

        return Task.CompletedTask;
    }
}
