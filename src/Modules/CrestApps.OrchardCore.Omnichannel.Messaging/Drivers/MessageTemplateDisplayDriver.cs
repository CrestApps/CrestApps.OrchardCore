using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// The display-management driver for <see cref="MessageTemplate"/>: the admin list row and the create/edit form.
/// </summary>
public sealed class MessageTemplateDisplayDriver : DisplayDriver<MessageTemplate>
{
    public override IDisplayResult Display(MessageTemplate template, BuildDisplayContext context)
    {
        return View("MessageTemplate_Fields_SummaryAdmin", template)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1");
    }

    public override IDisplayResult Edit(MessageTemplate template, BuildEditorContext context)
    {
        return Initialize<MessageTemplateViewModel>("MessageTemplateFields_Edit", model =>
        {
            model.Name = template.Name;
            model.Body = template.Body;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(MessageTemplate template, UpdateEditorContext context)
    {
        var model = new MessageTemplateViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        template.Name = model.Name?.Trim();
        template.Body = model.Body?.Trim();

        return Edit(template, context);
    }
}
