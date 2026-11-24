using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// The credential awards list.
/// </summary>
public class CredentialAwardListViewModel
{
    /// <summary>
    /// Gets or sets the filters.
    /// </summary>
    public CredentialAwardListOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the definitions offered in the filter.
    /// </summary>
    public IList<SelectListItem> Definitions { get; set; } = [];

    /// <summary>
    /// Gets or sets the states offered in the filter.
    /// </summary>
    public IList<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user may award by hand.
    /// </summary>
    public bool CanAward { get; set; }

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    public AdminListContext List { get; set; }
}
