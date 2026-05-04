using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class CampaignActionsListDisplayDriver : DisplayDriver<OmnichannelCampaign>
{
    private readonly ISourceCatalog<CampaignAction> _actionCatalog;
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly CampaignActionOptions _actionOptions;

    public CampaignActionsListDisplayDriver(
        ISourceCatalog<CampaignAction> actionCatalog,
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        IOptions<CampaignActionOptions> actionOptions)
    {
        _actionCatalog = actionCatalog;
        _dispositionsCatalog = dispositionsCatalog;
        _actionOptions = actionOptions.Value;
    }

    public override IDisplayResult Edit(OmnichannelCampaign campaign, BuildEditorContext context)
    {
        if (context.IsNew)
        {
            return Initialize<CampaignActionsListViewModel>("CampaignActionsList_Edit", model =>
            {
                model.CampaignId = null;
                model.Actions = [];
                model.ActionTypes = [];
            }).Location("Content:100");
        }

        return Initialize<CampaignActionsListViewModel>("CampaignActionsList_Edit", async model =>
        {
            model.CampaignId = campaign.ItemId;
            model.ActionTypes = _actionOptions.ActionTypes.Values;

            var allActions = await _actionCatalog.GetAllAsync();

            var campaignActions = allActions
                .Where(a => string.Equals(a.CampaignId, campaign.ItemId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.DispositionId)
                .ThenBy(a => a.Source);

            var dispositions = await _dispositionsCatalog.GetAllAsync();
            var dispositionMap = dispositions.ToDictionary(d => d.ItemId, d => d.DisplayText, StringComparer.OrdinalIgnoreCase);

            foreach (var action in campaignActions)
            {
                dispositionMap.TryGetValue(action.DispositionId ?? string.Empty, out var dispositionText);

                var typeDisplayName = _actionOptions.ActionTypes.TryGetValue(action.Source, out var typeEntry)
                    ? typeEntry.DisplayName?.Value
                    : action.Source;

                model.Actions.Add(new CampaignActionEntryViewModel
                {
                    Model = action,
                    DispositionDisplayText = dispositionText ?? action.DispositionId,
                    ActionTypeDisplayName = typeDisplayName ?? action.Source,
                });
            }
        }).Location("Content:100");
    }
}
