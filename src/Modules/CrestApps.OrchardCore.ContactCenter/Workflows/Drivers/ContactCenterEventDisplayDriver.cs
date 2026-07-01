using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="ContactCenterEvent"/> workflow activity.
/// </summary>
public sealed class ContactCenterEventDisplayDriver : ActivityDisplayDriver<ContactCenterEvent, ContactCenterEventViewModel>
{
    /// <inheritdoc/>
    protected override void EditActivity(ContactCenterEvent activity, ContactCenterEventViewModel model)
    {
        model.EventType = activity.EventType;
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
