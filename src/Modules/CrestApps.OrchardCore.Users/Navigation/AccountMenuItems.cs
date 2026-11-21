using OrchardCore.DisplayManagement.Zones;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users.Navigation;

/// <summary>
/// Puts the items of the account menu in order. Orchard Core's navigation manager merges the providers of a named menu
/// in the order they were registered and leaves the sorting to the Navigation shape, which the account pages do not
/// render, so without this "Payment methods" or "Account members" came before "Overview".
/// </summary>
public static class AccountMenuItems
{
    /// <summary>
    /// Returns the items in order of their positions, with items of the same position in registration order and items
    /// without a position last.
    /// </summary>
    /// <param name="items">The items Orchard Core built.</param>
    /// <returns>The items in order.</returns>
    public static List<MenuItem> Arrange(IEnumerable<MenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .OrderBy(item => string.IsNullOrEmpty(item.Position))
            .ThenBy(item => item.Position, FlatPositionComparer.Instance)
            .ToList();
    }
}
