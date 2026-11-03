using CrestApps.OrchardCore.Scheduling.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// The editor of an appointment workflow event.
/// </summary>
/// <typeparam name="TActivity">The event type.</typeparam>
public sealed class AppointmentEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, AppointmentEventActivityViewModel>
    where TActivity : AppointmentEventActivity
{
    private readonly AppointmentTypeStore _typeStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="typeStore">The appointment type store.</param>
    public AppointmentEventActivityDisplayDriver(AppointmentTypeStore typeStore)
    {
        _typeStore = typeStore;
    }

    /// <inheritdoc />
    protected override async ValueTask EditActivityAsync(TActivity activity, AppointmentEventActivityViewModel model)
    {
        model.AppointmentTypeIds = activity.AppointmentTypeIds.ToArray();
        model.MinutesBefore = activity.MinutesBefore;
        model.HasMinutesBefore = activity is AppointmentStartingSoonEvent;
        model.TypeOptions = (await _typeStore.ListAsync()).Select(type => new SelectListItem(type.Name, type.ItemId, model.AppointmentTypeIds.Contains(type.ItemId))).ToList();
    }

    /// <inheritdoc />
    protected override void UpdateActivity(AppointmentEventActivityViewModel model, TActivity activity)
    {
        activity.AppointmentTypeIds = (model.AppointmentTypeIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        activity.MinutesBefore = Math.Clamp(model.MinutesBefore, 0, 43200);
    }
}
