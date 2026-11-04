using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.ViewModels;

/// <summary>
/// The Orders tab of an account's page.
/// </summary>
public class AccountOrdersTabViewModel
{
    public int Count { get; set; }

    public IReadOnlyList<Order> Orders { get; set; } = [];
}
