using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C8: the AP2 checkout mandate is checked inside the agent checkout's completion, after the checkout's final
/// state is known, for every transport (REST, MCP, A2A). A checkout made under AP2 needs a mandate whatever a later
/// request negotiates, the mandate must match where the order ships and how, and a complete request may not change
/// the checkout the buyer approved.
/// </summary>
public sealed class Ap2CompletionGateTests
{
    private const string Version = "2026-08-25";

    private readonly EcJsonWebKey _userKey = Ap2TestMandates.NewKey("user-1");
    private readonly EcJsonWebKey _agentKey = Ap2TestMandates.NewKey("agent-1");
    private readonly EcJsonWebKey _merchantKey = Ap2TestMandates.NewKey("merchant-1");

    [Fact]
    public async Task CheckAsync_CheckoutMadeUnderAp2_WithoutAMandate_IsRefused()
    {
        // Arrange: made under AP2, completed by a request (such as MCP) that negotiates nothing and sends no mandate.
        var checkout = CreateCheckout(ap2Required: true);
        var context = CreateContext(checkout, CreateView(Address("1 Main St")), mandate: null, negotiatedAp2: false);

        // Act
        var refusal = await CreateGate().CheckAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Equal(Ap2ErrorCodes.MandateRequired, refusal.Code);
    }

    [Fact]
    public async Task CheckAsync_MandateForAnotherShippingAddress_IsRefused()
    {
        // Arrange: the buyer approved shipping to 1 Main St; the checkout now ships elsewhere.
        var approved = CreateView(Address("1 Main St"));
        var mandate = CreateMandate(approved);
        var context = CreateContext(CreateCheckout(ap2Required: true), CreateView(Address("99 Other Rd")), mandate, negotiatedAp2: true);

        // Act
        var refusal = await CreateGate().CheckAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Equal(Ap2ErrorCodes.MandateScopeMismatch, refusal.Code);
    }

    [Fact]
    public async Task CheckAsync_MandateForTheCheckoutAsItStands_IsAccepted()
    {
        // Arrange
        var view = CreateView(Address("1 Main St"));
        var context = CreateContext(CreateCheckout(ap2Required: true), view, CreateMandate(view), negotiatedAp2: true);

        // Act
        var refusal = await CreateGate().CheckAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
    }

    [Fact]
    public async Task CheckChangesAsync_ACompleteRequestChangingAnAp2Checkout_IsRefused()
    {
        // Arrange
        var checkout = CreateCheckout(ap2Required: true);
        var context = CreateContext(checkout, view: null, mandate: null, negotiatedAp2: true);

        context.Update = new AgentCheckoutUpdate
        {
            Fulfillment = new AgentFulfillmentInput { Address = Address("99 Other Rd") },
        };

        // Act
        var refusal = await CreateGate().CheckChangesAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
    }

    [Fact]
    public async Task CheckAsync_CheckoutNotMadeUnderAp2_NeedsNoMandate()
    {
        // Arrange
        var context = CreateContext(CreateCheckout(ap2Required: false), CreateView(Address("1 Main St")), mandate: null, negotiatedAp2: false);

        // Act
        var refusal = await CreateGate().CheckAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
    }

    private Ap2CompletionGate CreateGate()
    {
        var keys = new Mock<IMerchantSigningKeyService>();

        keys.Setup(service => service.GetPublishedKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Ap2TestMandates.PublicOf(_merchantKey)]);

        return new Ap2CompletionGate(keys.Object, Mock.Of<IClock>(clock => clock.UtcNow == DateTimeOffset.FromUnixTimeSeconds(Ap2TestMandates.Now).UtcDateTime));
    }

    private static AgentCheckout CreateCheckout(bool ap2Required)
    {
        var checkout = new AgentCheckout { ItemId = "chk_test", PlatformId = "platform-1", Status = AgentCheckoutStatus.ReadyForComplete };

        if (ap2Required)
        {
            checkout.SetProperty(Ap2CompletionGate.RequiredProperty, "true");
        }

        return checkout;
    }

    private AgentCheckoutCompletionContext CreateContext(AgentCheckout checkout, AgentCheckoutView view, string mandate, bool negotiatedAp2)
    {
        var context = new AgentCheckoutContext
        {
            PlatformId = "platform-1",
            Protocol = AgenticCommerceConstants.Protocols.Ucp,
            ProtocolVersion = Version,
            PlatformProfile = new JsonObject { ["keys"] = new JsonArray(Ap2TestMandates.PublicOf(_userKey).ToJson()) },
        };

        if (negotiatedAp2)
        {
            context.Properties[Ap2CompletionGate.RequiredProperty] = "true";
        }

        var payment = new AgentPayment { HandlerId = "card" };

        if (mandate is not null)
        {
            payment.Properties[Ap2CheckoutExtension.MandateProperty] = mandate;
        }

        return new AgentCheckoutCompletionContext
        {
            Context = context,
            Checkout = checkout,
            Payment = payment,
            View = view,
        };
    }

    private static Address Address(string line1)
        => new() { AddressLine1 = line1, City = "Springfield", PostalCode = "12345", Country = "US" };

    private static AgentCheckoutView CreateView(Address shipTo)
        => new()
        {
            Id = "chk_test",
            Status = AgentCheckoutStatus.ReadyForComplete,
            Currency = "USD",
            Lines = [new AgentCheckoutLineView { Id = "li_1", ItemId = "SKU-A", Title = "Widget", Quantity = 2 }],
            Totals = new AgentCheckoutTotals { Subtotal = 10m, Total = 10m },
            Fulfillment = new AgentFulfillmentView
            {
                Required = true,
                Address = shipTo,
                Options = [new AgentFulfillmentOptionView { Id = "standard", Title = "Standard", Amount = 0m }],
                SelectedOptionId = "standard",
            },
        };

    // The buyer's mandate over the checkout the store signed: its items and total, and where and how it ships.
    private string CreateMandate(AgentCheckoutView approved)
    {
        var checkout = Ap2TestMandates.SampleCheckout();

        checkout["fulfillment"] = UcpCheckoutMapper.WriteFulfillment(approved, Version);

        var open = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey);

        return Ap2TestMandates.PresentClosedMandate(open, _agentKey, Ap2TestMandates.CreateCheckoutJwt(checkout, _merchantKey));
    }
}
