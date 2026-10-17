using OrchardCore.Admin;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// The agent entitlements page.
/// </summary>
public class AgentEntitlementListViewModel
{
    /// <summary>
    /// Gets or sets the agents the <c>admin-list</c> tag renders, in the layout (List or Grid) the site or the user
    /// picked.
    /// </summary>
    public AdminListContext List { get; set; }
}
