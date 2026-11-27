using CrestApps.OrchardCore.Scheduling.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentEventActivityDisplayDriver(
        AppointmentTypeStore typeStore,
        IStringLocalizer<AppointmentEventActivityDisplayDriver<TActivity>> stringLocalizer)
    {
        _typeStore = typeStore;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
        => Initialize<AppointmentEventActivityViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));

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
