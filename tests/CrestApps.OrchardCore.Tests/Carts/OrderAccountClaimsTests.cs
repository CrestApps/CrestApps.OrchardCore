using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// The token on a guest's order confirmation names one order for two hours, and can't be altered or reused from
/// another purpose's token.
/// </summary>
public sealed class OrderAccountClaimsTests
{
    private readonly EphemeralDataProtectionProvider _protection = new();
    private DateTime _now = DateTime.UtcNow;

    [Fact]
    public void ATokenNamesItsOrder()
    {
        var claims = Create();

        Assert.Equal("order-1", claims.Read(claims.Create("order-1")));
    }

    [Fact]
    public void AnAlteredOrMissingTokenNamesNothing()
    {
        var claims = Create();
        var token = claims.Create("order-1");

        Assert.Null(claims.Read(token[..^2] + (token[^2] == 'A' ? "BB" : "AA")));
        Assert.Null(claims.Read(null));
        Assert.Null(claims.Read(string.Empty));
    }

    [Fact]
    public void ATokenExpiresAfterTwoHours()
    {
        var claims = Create();
        var token = claims.Create("order-1");

        _now = _now.AddHours(2).AddMinutes(1);

        Assert.Null(claims.Read(token));
    }

    [Fact]
    public void ARecoveryLinkTokenIsNotAnAccountClaim()
    {
        var recovery = _protection.CreateProtector("CrestApps.OrchardCore.Orders.Recovery").ToTimeLimitedDataProtector();

        Assert.Null(Create().Read(recovery.Protect("order-1", _now.AddDays(1))));
    }

    [Fact]
    public void AnonymizingAnOrderRemovesItsCheckoutDetails()
    {
        var order = new Order
        {
            CustomerEmail = "jordan@example.test",
            CheckoutDetails = new System.Text.Json.Nodes.JsonObject { ["CheckoutDetails"] = new System.Text.Json.Nodes.JsonObject { ["GiftMessage"] = "For Sam" } },
        };

        order.Anonymize(_now);

        Assert.Null(order.CheckoutDetails);
        Assert.Null(order.CustomerEmail);
    }

    private OrderAccountClaims Create()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        return new OrderAccountClaims(_protection, clock.Object);
    }
}
