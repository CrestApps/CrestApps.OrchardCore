using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class CampaignActionDisplayDriver : DisplayDriver<CampaignAction>
{
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;

    internal readonly IStringLocalizer S;

    public CampaignActionDisplayDriver(
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        IStringLocalizer<CampaignActionDisplayDriver> stringLocalizer)
    {
        _dispositionsCatalog = dispositionsCatalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(CampaignAction action, BuildDisplayContext context)
    {
        return CombineAsync(
            View("CampaignAction_Fields_SummaryAdmin", action)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("CampaignAction_Buttons_SummaryAdmin", action)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5")
        );
    }

    public override IDisplayResult Edit(CampaignAction action, BuildEditorContext context)
    {
        return Initialize<CampaignActionViewModel>("CampaignActionFields_Edit", async model =>
        {
            model.DispositionId = action.DispositionId;
            model.SetDoNotCall = action.SetDoNotCall;
            model.SetDoNotSms = action.SetDoNotSms;
            model.SetDoNotEmail = action.SetDoNotEmail;
            model.SetDoNotChat = action.SetDoNotChat;

            var dispositions = await _dispositionsCatalog.GetAllAsync();

            model.Dispositions = dispositions
                .Select(d => new SelectListItem
                {
                    Text = d.DisplayText,
                    Value = d.ItemId,
                    Selected = string.Equals(d.ItemId, action.DispositionId, StringComparison.OrdinalIgnoreCase),
                })
                .OrderBy(x => x.Text);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(CampaignAction action, UpdateEditorContext context)
    {
        var model = new CampaignActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DispositionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DispositionId), S["Disposition is a required field."]);
        }

        action.DispositionId = model.DispositionId;
        action.SetDoNotCall = model.SetDoNotCall;
        action.SetDoNotSms = model.SetDoNotSms;
        action.SetDoNotEmail = model.SetDoNotEmail;
        action.SetDoNotChat = model.SetDoNotChat;

        return Edit(action, context);
    }
}
