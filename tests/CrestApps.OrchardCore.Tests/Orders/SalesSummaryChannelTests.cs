using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Dashboard;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// The Sales Summary widget counts the orders of the channel it is set to, or all of them.
/// </summary>
public sealed class SalesSummaryChannelTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("pos", null, true)]
    [InlineData("", "pos", false)]
    [InlineData(null, "online", true)]
    [InlineData("online", "online", true)]
    [InlineData("pos", "online", false)]
    [InlineData("pos", "pos", true)]
    [InlineData("draft", "pos", false)]
    public void AnOrderCounts_WhenItsChannelIsTheOneChosen_OrNoneIsChosen(string orderChannel, string chosen, bool counts)
    {
        // Arrange
        var order = new Order { Channel = orderChannel };

        // Act
        var result = SalesSummaryPartDisplayDriver.InChannel(order, chosen);

        // Assert
        Assert.Equal(counts, result);
    }
}
