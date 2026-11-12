using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Teams.ViewModels;

/// <summary>
/// The Teams card of the staff editor.
/// </summary>
public class StaffTeamsEditViewModel
{
    public IList<StaffTeamMembershipRowViewModel> Memberships { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];
}

/// <summary>
/// One team membership row of the staff editor.
/// </summary>
public class StaffTeamMembershipRowViewModel
{
    public string TeamId { get; set; }

    public string Role { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}
