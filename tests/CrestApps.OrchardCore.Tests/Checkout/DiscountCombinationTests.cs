using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Discounts say which kinds of discount they combine with; when they can't all be used together, the combination
/// that takes off the most is kept, and the others give way.
/// </summary>
public sealed class DiscountCombinationTests
{
    [Fact]
    public void DiscountsThatCombine_AreAllKept()
    {
        var product = Line("SHIRTS", 10, DiscountClass.Product, DiscountCombination.Order);
        var order = Line("WELCOME", 5, DiscountClass.Order, DiscountCombination.Product);

        var (kept, skipped) = DefaultCheckoutDiscountService.Choose([product, order]);

        Assert.Equal([product, order], kept);
        Assert.Empty(skipped);
    }

    [Fact]
    public void WhenDiscountsCantBeCombined_TheMostTakenOffWins_AndTheOthersSayWhatTheyGaveWayTo()
    {
        var small = Line("TEN", 10, DiscountClass.Order, DiscountCombination.None);
        var large = Line("TWENTY", 20, DiscountClass.Order, DiscountCombination.None);
        var shipping = Line("FREESHIP", 7, DiscountClass.Shipping, DiscountCombination.Order);

        var (kept, skipped) = DefaultCheckoutDiscountService.Choose([small, large, shipping]);

        // Free shipping combines with order discounts, but neither order discount combines with shipping ones.
        Assert.Equal([large], kept);
        Assert.Equal([small, shipping], skipped);
        Assert.Equal("TWENTY", small.GaveWayTo);
        Assert.Equal("TWENTY", shipping.GaveWayTo);
    }

    [Fact]
    public void TwoSmallerDiscountsThatCombine_BeatOneLargerThatDoesnt()
    {
        var product = Line("SHIRTS", 8, DiscountClass.Product, DiscountCombination.Shipping);
        var shipping = Line("FREESHIP", 7, DiscountClass.Shipping, DiscountCombination.Product);
        var order = Line("BIG", 12, DiscountClass.Order, DiscountCombination.None);

        var (kept, _) = DefaultCheckoutDiscountService.Choose([product, shipping, order]);

        Assert.Equal([product, shipping], kept);
    }

    [Fact]
    public void ADiscountThatDoesntSay_CombinesWithEverything()
    {
        var line = new DiscountLine { Description = "Legacy", Amount = 1 };

        Assert.Equal(DiscountCombination.All, line.CombinesWith);
        Assert.Equal(DiscountClass.Order, line.GetClass());
        Assert.Equal(DiscountClass.Product, new DiscountLine { LineItemIds = ["a"] }.GetClass());
        Assert.Equal(DiscountClass.Shipping, new DiscountLine { Target = DiscountTarget.Shipping }.GetClass());
    }

    [Fact]
    public void ACoupon_IsAShippingProductOrOrderDiscount_AndCombinesWithWhatItsTicked()
    {
        Assert.Equal(DiscountClass.Shipping, new Coupon { Kind = CouponKind.FreeShipping }.DiscountClass);
        Assert.Equal(DiscountClass.Product, new Coupon { Kind = CouponKind.Percentage, ProductIds = ["p"] }.DiscountClass);
        Assert.Equal(DiscountClass.Order, new Coupon { Kind = CouponKind.FixedAmount }.DiscountClass);
        Assert.Equal(DiscountCombination.Product | DiscountCombination.Shipping, new Coupon { CombinesWithProductDiscounts = true, CombinesWithShippingDiscounts = true }.CombinesWith);
        Assert.Equal(DiscountCombination.None, new Coupon().CombinesWith);
    }

    [Fact]
    public void ACheckout_CarriesSeveralCodes_UpToItsLimit_IncludingOneSavedBeforeItCould()
    {
        var applied = new CheckoutCoupon { Code = "OLD" };

        Assert.True(applied.Add("new"));
        Assert.True(applied.Add("NEW"));
        Assert.Equal(["OLD", "new"], applied.GetCodes());

        Assert.True(applied.Remove("old"));
        Assert.Equal(["new"], applied.GetCodes());

        for (var i = 0; i < CheckoutCoupon.MaxCodes - 1; i++)
        {
            Assert.True(applied.Add("code" + i));
        }

        Assert.False(applied.Add("one-too-many"));
        Assert.Equal(CheckoutCoupon.MaxCodes, applied.GetCodes().Count);
    }

    private static DiscountLine Line(string description, decimal amount, DiscountClass discountClass, DiscountCombination combinesWith)
        => new() { Code = description, Description = description, Amount = amount, Class = discountClass, CombinesWith = combinesWith };
}
