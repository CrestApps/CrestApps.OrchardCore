using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// The display-management driver for <see cref="MessageTemplate"/>: the admin list row and the create/edit form.
/// </summary>
public sealed class MessageTemplateDisplayDriver : DisplayDriver<MessageTemplate>
{
    public override IDisplayResult Display(MessageTemplate template, BuildDisplayContext context)
    {
        // A template is only displayed as a row of the admin list of templates.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = template.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("MessageTemplate_Checkbox_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("MessageTemplate_Fields_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("MessageTemplate_Description_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(template.Body))),
            View("MessageTemplate_Buttons_SummaryAdmin", template)
                .Location("Actions:5"));
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
