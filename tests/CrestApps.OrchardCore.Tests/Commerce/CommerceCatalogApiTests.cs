using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Inventory.Api;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Products.Api;
using CrestApps.OrchardCore.Products.Core.Services;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// What the catalog and inventory APIs say about stock: the status a product page shows, and a count only when few are
/// left, so a competitor can't read the store's stock levels off the API.
/// </summary>
public sealed class CommerceCatalogApiTests
{
    [Fact]
    public void AProductNobodyCounts_IsInStock()
    {
        var availability = CatalogApiProductReader.ToAvailability(null);

        Assert.Equal("inStock", availability.Status);
        Assert.True(availability.CanBuy);
        Assert.Null(availability.Available);
    }

    [Theory]
    [InlineData(50, false, "inStock", null, true)]
    [InlineData(3, false, "lowStock", 3, true)]
    [InlineData(0, false, "outOfStock", null, false)]
    [InlineData(0, true, "backorder", null, true)]
    public void TheCatalog_GivesTheStatus_AndTheCountOnlyWhenLow(int available, bool backorder, string status, int? count, bool canBuy)
    {
        var availability = CatalogApiProductReader.ToAvailability(new ProductAvailability(available, backorder, LowStockThreshold: 5));

        Assert.Equal(status, availability.Status);
        Assert.Equal(count, availability.Available);
        Assert.Equal(canBuy, availability.CanBuy);
    }

    [Fact]
    public void AReleaseDate_MakesABackorderAPreorder()
    {
        var release = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var availability = CatalogApiProductReader.ToAvailability(new ProductAvailability(0, true, 5) { ReleaseDateUtc = release });

        Assert.Equal("preorder", availability.Status);
        Assert.Equal(release, availability.ReleaseDateUtc);
    }

    [Fact]
    public void EachLocation_IsAnsweredFromItsOwnLevel()
    {
        var location = new InventoryLocation { LocationId = "downtown", Name = "Downtown store", Address = "400 Pine St" };
        var item = new StockItem { LowStockThreshold = 2 };

        var plenty = InventoryApiEndpoints.ToAvailability(location, item, new StockLocationLevel { LocationId = "downtown", OnHand = 10, Reserved = 1 });
        var few = InventoryApiEndpoints.ToAvailability(location, item, new StockLocationLevel { LocationId = "downtown", OnHand = 3, Reserved = 1 });
        var none = InventoryApiEndpoints.ToAvailability(location, item, level: null);

        Assert.Equal(("inStock", (int?)null, true), (plenty.Status, plenty.Available, plenty.CanBuy));
        Assert.Equal(("lowStock", (int?)2, true), (few.Status, few.Available, few.CanBuy));
        Assert.Equal(("outOfStock", (int?)null, false), (none.Status, none.Available, none.CanBuy));
        Assert.Equal("Downtown store", plenty.Name);
    }

    [Fact]
    public void AProductNobodyCounts_IsInStockAtEveryLocation()
    {
        var availability = InventoryApiEndpoints.ToAvailability(new InventoryLocation { LocationId = "default" }, item: null, level: null);

        Assert.Equal("inStock", availability.Status);
        Assert.True(availability.CanBuy);
    }

    [Fact]
    public void Money_WithoutACurrency_IsNotMoney()
    {
        Assert.Null(ApiMoney.From(0m, null));
        Assert.Null(ApiMoney.From((decimal?)null, "USD"));
        Assert.Equal(new ApiMoney(10.13m, "USD"), ApiMoney.From(10.126m, "USD"));
        Assert.Equal(1235m, ApiMoney.From(1234.6m, "JPY").Amount);
    }
}
