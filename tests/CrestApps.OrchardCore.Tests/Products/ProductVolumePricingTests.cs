using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A buyer gets the best single volume or role discount they qualify for, and a cart line follows the product's
/// minimum and pack size.
/// </summary>
public sealed class ProductVolumePricingTests
{
    private static readonly ProductPart _product = new()
    {
        Price = 100m,
        PriceTiers = [new() { MinQuantity = 5, PercentOff = 10m }, new() { MinQuantity = 10, PercentOff = 15m }],
        RoleDiscounts = [new() { Role = "Wholesale", PercentOff = 20m }],
    };

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 10)]
    [InlineData(12, 15)]
    public void TheBestTierReachedApplies(int quantity, decimal percentOff)
        => Assert.Equal(percentOff, ProductVolumePricing.GetPercentOff(_product, quantity, []));

    [Fact]
    public void ARoleDiscountBeatsASmallerTier_AndDiscountsDontAddUp()
    {
        Assert.Equal(20m, ProductVolumePricing.GetPercentOff(_product, 12, ["wholesale"]));
        Assert.Equal(15m, ProductVolumePricing.GetPercentOff(_product, 12, ["Customer"]));
    }

    [Fact]
    public void TheDiscountIsRoundedToCents()
        => Assert.Equal(26.99m, ProductVolumePricing.Apply(29.99m, 10m));

    [Theory]
    [InlineData(2, ProductQuantityRule.BelowMinimum)]
    [InlineData(4, ProductQuantityRule.NotAMultiple)]
    [InlineData(6, ProductQuantityRule.None)]
    public void ALineFollowsTheMinimumAndPackSize(int quantity, ProductQuantityRule rule)
        => Assert.Equal(rule, ProductVolumePricing.Check(new ProductPart { MinQuantity = 3, QuantityStep = 3 }, quantity));

    [Fact]
    public void TheQuantityBoxStartsAtTheMinimumRoundedUpToAPack()
    {
        Assert.Equal(6, ProductVolumePricing.GetStartingQuantity(new ProductPart { MinQuantity = 4, QuantityStep = 3 }));
        Assert.Equal(1, ProductVolumePricing.GetStartingQuantity(new ProductPart()));
    }
}
