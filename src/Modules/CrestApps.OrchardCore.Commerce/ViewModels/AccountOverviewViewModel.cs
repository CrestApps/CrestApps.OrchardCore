using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Commerce.ViewModels;

/// <summary>
/// The storefront account's home page: the pages the modules add to the account menu, one tile each. It is the
/// model of the <c>AccountOverview</c> shape, so it stays unsealed for the shape's proxy.
/// </summary>
public class AccountOverviewViewModel
{
    /// <summary>
    /// Gets or sets the account menu's pages, without the home page itself.
    /// </summary>
    public IReadOnlyList<MenuItem> Items { get; set; } = [];
}
