using CrestApps.OrchardCore.Users.Navigation;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Users;

/// <summary>
/// Orchard Core's navigation manager merges the account menu's providers in registration order and never sorts the
/// result, so the account page and its menu put the items in order of their positions themselves.
/// </summary>
public sealed class AccountMenuItemsTests
{
    [Fact]
    public void Arrange_PutsTheItemsInOrderOfTheirPositions()
    {
        var items = new List<MenuItem>
        {
            Item("Payment methods", "60"),
            Item("Account members", "31"),
            Item("Overview", "0"),
            Item("My transcript", "30.4"),
            Item("My learning", "30"),
            Item("Orders", "10"),
            Item("Notifications", "90"),
        };

        var arranged = AccountMenuItems.Arrange(items);

        Assert.Equal(
            ["Overview", "Orders", "My learning", "My transcript", "Account members", "Payment methods", "Notifications"],
            arranged.Select(item => item.Text.Value));
    }

    [Fact]
    public void Arrange_KeepsTheRegistrationOrderOfItemsWithTheSamePosition()
    {
        var items = new List<MenuItem>
        {
            Item("Subscriptions", "30"),
            Item("My learning", "30"),
            Item("No position", null),
        };

        var arranged = AccountMenuItems.Arrange(items);

        Assert.Equal(["Subscriptions", "My learning", "No position"], arranged.Select(item => item.Text.Value));
    }

    private static MenuItem Item(string text, string position)
        => new()
        {
            Text = new LocalizedString(text, text),
            Position = position,
            Href = "/" + text,
        };
}
