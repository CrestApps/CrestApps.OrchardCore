using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Commerce.IntegrationTests;

/// <summary>
/// A guest buying over the commerce API, end to end: the catalog, the cart, the checkout's steps, paying and placing
/// the order; and the guest tokens that keep one guest's cart and checkout from another.
/// </summary>
[Collection(CommerceApiCollection.Name)]
public sealed class GuestCheckoutApiTests
{
    private static readonly object _address = new
    {
        name = "Api Buyer",
        addressLine1 = "500 Pine St",
        city = "Seattle",
        region = "WA",
        postalCode = "98101",
        country = "US",
    };

    private readonly CommerceApiFixture _fixture;

    public GuestCheckoutApiTests(CommerceApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheCatalog_ListsAndGetsProducts_PricedForAGuest()
    {
        using var client = _fixture.CreateClient();

        var list = await client.GetAsync("products?take=5&sort=price-asc");

        Assert.Equal(200, list.Status);
        Assert.True(list.Json["total"].GetValue<int>() >= 5, list.ToString());

        var first = list.Json["items"][0];
        var prices = list.Json["items"].AsArray().Select(item => item["price"]?["amount"]?.GetValue<decimal>() ?? 0m).ToArray();

        Assert.Equal(prices.OrderBy(price => price), prices);

        var product = await client.GetAsync($"products/{first["id"]}");

        Assert.Equal(200, product.Status);
        Assert.Equal(first["title"].GetValue<string>(), product.Json["title"].GetValue<string>());
        Assert.NotNull(product.Json["availability"]["status"]);

        var missing = await client.GetAsync("products/no-such-product");

        Assert.Equal(404, missing.Status);
        Assert.Equal("Not found", missing.Json["title"].GetValue<string>());
    }

    [Fact]
    public async Task AGuestsCart_TravelsInItsToken_AndNobodyElseSeesIt()
    {
        using var guest = _fixture.CreateClient();
        using var stranger = _fixture.CreateClient();
        var productId = await FindPurchasableAsync(guest);

        var added = await guest.PostAsync("cart/lines", new { productId, quantity = 2 });

        Assert.Equal(200, added.Status);
        Assert.NotNull(guest.CartToken);
        Assert.Equal(2, added.Json["itemCount"].GetValue<int>());

        var lineId = added.Json["lines"][0]["lineId"].GetValue<string>();
        var changed = await guest.PatchAsync($"cart/lines/{lineId}", new { quantity = 3 });

        Assert.Equal(3, changed.Json["itemCount"].GetValue<int>());

        // Without the token there is no cart, and another cart's line can't be touched.
        Assert.Equal(0, (await stranger.GetAsync("cart")).Json["itemCount"].GetValue<int>());
        Assert.Equal(404, (await stranger.DeleteAsync($"cart/lines/{lineId}")).Status);

        var removed = await guest.DeleteAsync($"cart/lines/{lineId}");

        Assert.Equal(0, removed.Json["itemCount"].GetValue<int>());
    }

    [Fact]
    public async Task AGuest_ChecksOutOverTheApi_AndTheOrderIsPaid()
    {
        using var guest = _fixture.CreateClient();
        var productId = await FindPurchasableAsync(guest);

        Assert.Equal(200, (await guest.PostAsync("cart/lines", new { productId, quantity = 1 })).Status);

        var key = Guid.NewGuid().ToString();
        var started = await guest.PostAsync("checkout", idempotencyKey: key);

        Assert.Equal(201, started.Status);
        Assert.NotNull(guest.CheckoutToken);

        var checkoutId = started.Json["id"].GetValue<string>();
        var orderId = started.Json["orderId"].GetValue<string>();

        // Sent again with the same key, it's the same checkout, not a second order.
        var again = await guest.PostAsync("checkout", idempotencyKey: key);

        Assert.Equal(checkoutId, again.Json["id"].GetValue<string>());

        var invalid = await guest.PutAsync($"checkout/{checkoutId}/contact", new { name = "", email = "not-an-email" });

        Assert.Equal(400, invalid.Status);
        Assert.NotNull(invalid.Json["errors"]["name"]);
        Assert.NotNull(invalid.Json["errors"]["email"]);

        var contact = await guest.PutAsync($"checkout/{checkoutId}/contact", new { name = "Api Buyer", email = "api.buyer@example.invalid", billingAddress = _address });

        Assert.Equal(200, contact.Status);

        var rates = await guest.PostAsync($"checkout/{checkoutId}/shipping-rates", new { address = _address });

        Assert.Equal(200, rates.Status);
        Assert.NotEmpty(rates.Json.AsArray());

        var shipping = await guest.PutAsync($"checkout/{checkoutId}/shipping", new { address = _address, rateId = rates.Json[0]["rateId"].GetValue<string>() });

        Assert.Equal(200, shipping.Status);
        Assert.Equal("Payment", shipping.Json["nextStep"].GetValue<string>());
        Assert.NotNull(shipping.Json["totals"]["shipping"]);

        var badCode = await guest.PostAsync($"checkout/{checkoutId}/codes", new { code = "NOT-A-CODE" });

        Assert.Equal(400, badCode.Status);

        var methods = await guest.GetAsync($"checkout/{checkoutId}/payment-methods");

        Assert.Contains(methods.Json.AsArray(), method => method["key"].GetValue<string>() == "test");

        // Another guest can't see, pay or complete this checkout.
        using var stranger = _fixture.CreateClient();

        Assert.Equal(404, (await stranger.GetAsync($"checkout/{checkoutId}")).Status);
        Assert.Equal(404, (await stranger.PostAsync($"checkout/{checkoutId}/payment", new { paymentMethod = "test" })).Status);
        Assert.Equal(404, (await stranger.PostAsync($"checkout/{checkoutId}/complete")).Status);

        var paid = await guest.PostAsync($"checkout/{checkoutId}/payment", new { paymentMethod = "test" }, Guid.NewGuid().ToString());

        Assert.Equal(200, paid.Status);

        var completed = await guest.PostAsync($"checkout/{checkoutId}/complete");

        Assert.Equal(200, completed.Status);
        Assert.True(completed.Json["completed"].GetValue<bool>(), completed.ToString());
        Assert.Equal(orderId, completed.Json["orderId"].GetValue<string>());

        var order = await _fixture.InTenantAsync(services => services.GetRequiredService<IOrderStore>().FindByIdAsync(orderId).AsTask());

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal("api.buyer@example.invalid", order.CustomerEmail);
    }

    [Fact]
    public async Task AnEmptyCart_CantBeCheckedOut()
    {
        using var guest = _fixture.CreateClient();

        var started = await guest.PostAsync("checkout");

        Assert.Equal(409, started.Status);
        Assert.Equal("empty-cart", started.Json["code"].GetValue<string>());
    }

    [Fact]
    public async Task TheBuyersOwnEndpoints_NeedASignedInCaller()
    {
        using var guest = _fixture.CreateClient();

        Assert.Equal(401, (await guest.GetAsync("me")).Status);
        Assert.Equal(401, (await guest.GetAsync("me/orders")).Status);

        guest.Bearer = "not-a-token";

        Assert.Equal(401, (await guest.GetAsync("me")).Status);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("customers")]
    [InlineData("inventory")]
    [InlineData("coupons")]
    public async Task TheStoresOwnEndpoints_RefuseAGuest(string path)
    {
        using var guest = _fixture.CreateClient();

        Assert.Equal(401, (await guest.GetAsync(path)).Status);
    }

    /// <summary>
    /// Checks a simple product out as a guest, paid with the test provider, and returns the order's id.
    /// </summary>
    internal static async Task<string> CheckOutAsync(CommerceApiFixture fixture)
    {
        using var guest = fixture.CreateClient();
        var productId = await FindPurchasableAsync(guest);

        Assert.Equal(200, (await guest.PostAsync("cart/lines", new { productId, quantity = 1 })).Status);

        var started = await guest.PostAsync("checkout");
        var checkoutId = started.Json["id"].GetValue<string>();

        Assert.Equal(200, (await guest.PutAsync($"checkout/{checkoutId}/contact", new { name = "Api Buyer", email = "webhooks@example.invalid", billingAddress = _address })).Status);

        var rates = await guest.PostAsync($"checkout/{checkoutId}/shipping-rates", new { address = _address });

        Assert.Equal(200, (await guest.PutAsync($"checkout/{checkoutId}/shipping", new { address = _address, rateId = rates.Json[0]["rateId"].GetValue<string>() })).Status);
        Assert.Equal(200, (await guest.PostAsync($"checkout/{checkoutId}/payment", new { paymentMethod = "test" })).Status);

        var completed = await guest.PostAsync($"checkout/{checkoutId}/complete");

        Assert.True(completed.Json["completed"].GetValue<bool>(), completed.ToString());

        return completed.Json["orderId"].GetValue<string>();
    }

    private static async Task<string> FindPurchasableAsync(ApiClient client)
    {
        var list = await client.GetAsync("products?take=30");

        foreach (var item in list.Json["items"].AsArray())
        {
            if (!item["isPurchasable"].GetValue<bool>())
            {
                continue;
            }

            var product = await client.GetAsync($"products/{item["id"]}");

            // A product with variants or add-ons needs choices; a simple one in stock is enough here.
            if (product.Json["variants"].AsArray().Count == 0 &&
                product.Json["addOns"].AsArray().Count == 0 &&
                product.Json["prices"].AsArray().Count == 0 &&
                product.Json["availability"]["canBuy"].GetValue<bool>())
            {
                return item["id"].GetValue<string>();
            }
        }

        throw new InvalidOperationException("The store sample has no simple product in stock: " + list);
    }
}
