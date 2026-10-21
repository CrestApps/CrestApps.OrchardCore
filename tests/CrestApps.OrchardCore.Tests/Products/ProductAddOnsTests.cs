using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// The personal touches a buyer adds to a product: only the product's own are kept, a long text is cut, the
/// required ones are checked, and an order line reads with them.
/// </summary>
public sealed class ProductAddOnsTests
{
    private static readonly ProductPart _product = new()
    {
        AddOns =
        [
            new ProductAddOn { AddOnId = "name", Label = "Engraving", Kind = ProductAddOnKind.Text, Price = 5m, Required = true },
            new ProductAddOn { AddOnId = "wrap", Label = "Gift wrap", Kind = ProductAddOnKind.Checkbox, Price = 3m },
        ],
    };

    [Fact]
    public void Normalize_KeepsTheProductsOwn_TrimsText_AndDropsAnUntickedBox()
    {
        var chosen = ProductAddOns.Normalize(_product, new Dictionary<string, string> { ["name"] = " Sam ", ["wrap"] = "false", ["other"] = "x" });

        Assert.Equal(new Dictionary<string, string> { ["name"] = "Sam" }, chosen);
    }

    [Fact]
    public void Normalize_CutsALongText_AndGivesNullForNothingChosen()
    {
        var chosen = ProductAddOns.Normalize(_product, new Dictionary<string, string> { ["name"] = new string('a', 500), ["wrap"] = "on" });

        Assert.Equal(ProductAddOn.MaxTextLength, chosen["name"].Length);
        Assert.Equal(ProductAddOns.Checked, chosen["wrap"]);
        Assert.Null(ProductAddOns.Normalize(_product, new Dictionary<string, string> { ["name"] = " " }));
        Assert.Null(ProductAddOns.Normalize(new ProductPart(), new Dictionary<string, string> { ["name"] = "Sam" }));
    }

    [Fact]
    public void FindMissing_NamesTheRequiredOneLeftOut()
    {
        Assert.Equal("name", ProductAddOns.FindMissing(_product, null)?.AddOnId);
        Assert.Equal("name", ProductAddOns.FindMissing(_product, new Dictionary<string, string> { ["wrap"] = "true" })?.AddOnId);
        Assert.Null(ProductAddOns.FindMissing(_product, new Dictionary<string, string> { ["name"] = "Sam" }));
    }

    [Fact]
    public void GetSurcharge_AddsThePricesOfWhatWasChosen()
    {
        Assert.Equal(8m, ProductAddOns.GetSurcharge(_product, new Dictionary<string, string> { ["name"] = "Sam", ["wrap"] = "true" }));
        Assert.Equal(0m, ProductAddOns.GetSurcharge(_product, null));
    }

    [Fact]
    public void AreSame_ComparesTheChoices_WhateverTheirOrder()
    {
        Assert.True(ProductAddOns.AreSame(null, new Dictionary<string, string>()));
        Assert.True(ProductAddOns.AreSame(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }, new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" }));
        Assert.False(ProductAddOns.AreSame(new Dictionary<string, string> { ["a"] = "1" }, new Dictionary<string, string> { ["a"] = "2" }));
        Assert.False(ProductAddOns.AreSame(null, new Dictionary<string, string> { ["a"] = "1" }));
    }

    [Fact]
    public void AnOrderLine_ReadsWithItsAddOns()
    {
        var line = new OrderLine
        {
            Title = "Mug",
            PriceName = "Large",
            AddOns =
            [
                new OrderLineAddOn { Label = "Engraving", Value = "Sam", Price = 5m },
                new OrderLineAddOn { Label = "Gift wrap", Price = 3m },
            ],
        };

        Assert.Equal("Mug (Large) · Engraving: Sam · Gift wrap", OrderLineText.Describe(line));
        Assert.Equal("Mug", OrderLineText.Describe(new OrderLine { Title = "Mug" }));
    }
}
