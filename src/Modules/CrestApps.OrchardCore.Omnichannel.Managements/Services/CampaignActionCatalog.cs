using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class CampaignActionCatalog : SourceCatalog<CampaignAction>
{
    public CampaignActionCatalog(IDocumentManager<DictionaryDocument<CampaignAction>> documentManager)
        : base(documentManager)
    {
    }

    protected override IEnumerable<CampaignAction> GetSortable(CrestApps.Core.Models.QueryContext context, IEnumerable<CampaignAction> records)
    {
        if (context is CampaignActionQueryContext campaignContext)
        {
            if (!string.IsNullOrEmpty(campaignContext.CampaignId))
            {
                records = records.Where(x => string.Equals(x.CampaignId, campaignContext.CampaignId, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(campaignContext.DispositionId))
            {
                records = records.Where(x => string.Equals(x.DispositionId, campaignContext.DispositionId, StringComparison.OrdinalIgnoreCase));
            }
        }

        return base.GetSortable(context, records);
    }
}
