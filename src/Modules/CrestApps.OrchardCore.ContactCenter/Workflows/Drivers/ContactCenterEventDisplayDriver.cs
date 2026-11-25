using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="ContactCenterEvent"/> workflow activity.
/// </summary>
public sealed class ContactCenterEventDisplayDriver : ActivityDisplayDriver<ContactCenterEvent, ContactCenterEventViewModel>
{
    private readonly IContactCenterWorkflowEventTypeProvider _eventTypeProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="eventTypeProvider">The provider that supplies the selectable Contact Center event types.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterEventDisplayDriver(
        IContactCenterWorkflowEventTypeProvider eventTypeProvider,
        IStringLocalizer<ContactCenterEventDisplayDriver> stringLocalizer)
    {
        _eventTypeProvider = eventTypeProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ContactCenterEvent activity, BuildEditorContext context)
    {
        return Initialize<ContactCenterEventViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));
    }

    /// <inheritdoc/>
    protected override void EditActivity(ContactCenterEvent activity, ContactCenterEventViewModel model)
    {
        model.EventType = activity.EventType;
        model.EventTypes = _eventTypeProvider.GetEventTypes();
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterEvent activity, UpdateEditorContext context)
    {
        var model = new ContactCenterEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.EventType = model.EventType?.Trim();

        return Edit(activity, context);
    }
}
