using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.BusinessHours.Workflows;

/// <summary>
/// The editor of the Check Business Hours workflow task.
/// </summary>
public sealed class CheckBusinessHoursTaskDisplayDriver : ActivityDisplayDriver<CheckBusinessHoursTask, CheckBusinessHoursTaskViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckBusinessHoursTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckBusinessHoursTaskDisplayDriver(IStringLocalizer<CheckBusinessHoursTaskDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(CheckBusinessHoursTask activity, BuildEditorContext context)
        => Initialize<CheckBusinessHoursTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));

    /// <inheritdoc />
    protected override void EditActivity(CheckBusinessHoursTask activity, CheckBusinessHoursTaskViewModel model)
    {
        model.ScheduleId = activity.ScheduleId;
        model.TimeZoneExpression = activity.TimeZoneExpression;
    }

    /// <inheritdoc />
    protected override void UpdateActivity(CheckBusinessHoursTaskViewModel model, CheckBusinessHoursTask activity)
    {
        activity.ScheduleId = model.ScheduleId?.Trim();
        activity.TimeZoneExpression = model.TimeZoneExpression?.Trim();
    }
}
