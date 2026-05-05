using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class TryAgainCampaignActionViewModel
{
    public int? MaxAttempt { get; set; }

    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    public string NormalizedUserName { get; set; }

    public int? DefaultScheduleHours { get; set; }

    public IEnumerable<SelectListItem> SelectedUsers { get; set; }
}
