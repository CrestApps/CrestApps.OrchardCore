using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class CampaignActionsListViewModel
{
    public string CampaignId { get; set; }

    public IList<CampaignActionEntryViewModel> Actions { get; set; } = [];

    public IEnumerable<CampaignActionTypeEntry> ActionTypes { get; set; } = [];
}

public class CampaignActionEntryViewModel
{
    public CampaignAction Model { get; set; }

    public string DispositionDisplayText { get; set; }

    public string ActionTypeDisplayName { get; set; }
}
