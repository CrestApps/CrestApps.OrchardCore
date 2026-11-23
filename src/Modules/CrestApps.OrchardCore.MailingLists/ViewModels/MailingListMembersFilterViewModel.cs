using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// The status and source filters of a list's members page, rendered at the end of the list's toolbar.
/// </summary>
public class MailingListMembersFilterViewModel
{
    /// <summary>
    /// Gets or sets the states to choose from, the chosen one selected.
    /// </summary>
    public List<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the sources the members joined through, the chosen one selected.
    /// </summary>
    public List<SelectListItem> Sources { get; set; } = [];
}
