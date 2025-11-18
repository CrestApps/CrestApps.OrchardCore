using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Workflows;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;

internal sealed class CompletedActivityEventDisplayDriver : ActivityDisplayDriver<CompletedActivityEvent, CompletedActivityEventViewModel>
{
    private readonly ICatalog<OmnichannelCampaign> _catalog;

    internal readonly IStringLocalizer S;

    public CompletedActivityEventDisplayDriver(
        ICatalog<OmnichannelCampaign> catalog,
        IStringLocalizer<CompletedActivityEventDisplayDriver> stringLocalizer)
    {
        _catalog = catalog;
        S = stringLocalizer;
    }

    protected override async ValueTask EditActivityAsync(CompletedActivityEvent activity, CompletedActivityEventViewModel model)
    {
        model.CampaignId = activity.CampaignId;

        model.Campaigns = (await _catalog.GetAllAsync())
            .Select(x => new SelectListItem(x.DisplayText, x.ItemId))
            .OrderBy(x => x.Text);
    }

    public override async Task<IDisplayResult> UpdateAsync(CompletedActivityEvent activity, UpdateEditorContext context)
    {
        var model = new CompletedActivityEventViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.CampaignId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CampaignId), S["Campaign is required."]);
        }
        else if (await _catalog.FindByIdAsync(model.CampaignId) == null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CampaignId), S["Invalid campaign."]);
        }

        activity.CampaignId = model.CampaignId;

        return await EditAsync(activity, context);
    }
}
