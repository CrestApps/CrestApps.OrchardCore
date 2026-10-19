using CrestApps.OrchardCore.Orders.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// A shipment's packing slip link names its lines as "lineId:quantity" pairs; anything else in it is ignored.
/// </summary>
public sealed class OrderDocumentBuilderTests
{
    [Fact]
    public void ParseLines_ReadsLineIdsAndQuantities()
    {
        var lines = OrderDocumentBuilder.ParseLines("a1:2, b2:1,c3:0,bad,d4:x,:3");

        Assert.Equal(new Dictionary<string, int> { ["a1"] = 2, ["b2"] = 1 }, lines);
    }

    [Fact]
    public void ParseLines_GivesNullForNothing_SoTheWholeOrderIsPacked()
    {
        Assert.Null(OrderDocumentBuilder.ParseLines(null));
        Assert.Null(OrderDocumentBuilder.ParseLines("  "));
        Assert.Null(OrderDocumentBuilder.ParseLines("x:0"));
    }
}
