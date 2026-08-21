using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Telephony.Sms.Drivers;

/// <summary>
/// The display-management driver for <see cref="SmsTemplate"/>: the admin list row and the create/edit form.
/// </summary>
public sealed class SmsTemplateDisplayDriver : DisplayDriver<SmsTemplate>
{
    public override IDisplayResult Display(SmsTemplate template, BuildDisplayContext context)
    {
        return View("SmsTemplate_Fields_SummaryAdmin", template)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1");
    }

    public override IDisplayResult Edit(SmsTemplate template, BuildEditorContext context)
    {
        return Initialize<SmsTemplateViewModel>("SmsTemplateFields_Edit", model =>
        {
            model.Name = template.Name;
            model.Body = template.Body;
            model.Enabled = template.Enabled;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(SmsTemplate template, UpdateEditorContext context)
    {
        var model = new SmsTemplateViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        template.Name = model.Name?.Trim();
        template.Body = model.Body?.Trim();
        template.Enabled = model.Enabled;

        return Edit(template, context);
    }
}
