using CrestApps.OrchardCore.Products.Core.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// What a buyer is told about a product's stock: a release date to come makes a backorder a pre-order.
/// </summary>
public sealed class ProductAvailabilityTests
{
    private static readonly DateTime _release = new(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoneLeft_WithBackordersAndARelease_IsAPreorder()
    {
        var availability = new ProductAvailability(0, AllowBackorder: true, LowStockThreshold: 2) { ReleaseDateUtc = _release };

        Assert.Equal(ProductAvailabilityStatus.Preorder, availability.Status);
        Assert.True(availability.CanBuy(5));
    }

    [Fact]
    public void ARelease_DoesntChangeStockInHand_OrAnItemWithoutBackorders()
    {
        Assert.Equal(ProductAvailabilityStatus.InStock, new ProductAvailability(10, true, 2) { ReleaseDateUtc = _release }.Status);
        Assert.Equal(ProductAvailabilityStatus.OutOfStock, new ProductAvailability(0, false, 2) { ReleaseDateUtc = _release }.Status);
        Assert.Equal(ProductAvailabilityStatus.Backorder, new ProductAvailability(0, true, 2).Status);
    }
}
