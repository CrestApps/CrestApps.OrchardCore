namespace CrestApps.OrchardCore.Core.Models;

/// <summary>
/// The filters an admin list shows at the end of its toolbar, passed to the list as its <c>ToolbarActions</c>. The
/// selects name the list's filter form, so they submit it wherever the list is rendered.
/// </summary>
public class AdminListFiltersViewModel
{
    /// <summary>
    /// Gets or sets the list's name, such as <c>Coupons</c>, which names the shape's alternate.
    /// </summary>
    public string ListName { get; set; }

    /// <summary>
    /// Gets or sets the id of the form the filters submit.
    /// </summary>
    public string FormId { get; set; }

    /// <summary>
    /// Gets or sets the filters, in the order they're shown.
    /// </summary>
    public IReadOnlyList<AdminListFilter> Filters { get; set; } = [];
}
