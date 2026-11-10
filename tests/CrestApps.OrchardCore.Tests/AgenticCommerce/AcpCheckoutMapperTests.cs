using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Payments;
using Moq;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// ACP checkout JSON is read in both its current and legacy shapes, an update changes only what it sends, and the
/// answer's totals carry display texts with the discount as a positive amount.
/// </summary>
public sealed class AcpCheckoutMapperTests
{
    [Fact]
    public void ReadInput_ReadsLineItemsWithAQuantityThatDefaultsToOne()
    {
        // Arrange
        var body = JsonNode.Parse("""
            { "line_items": [ { "id": "p1", "quantity": 3 }, { "id": "p2" }, { "item": { "id": "p3", "quantity": 2 } } ] }
            """).AsObject();

        // Act
        var input = AcpCheckoutMapper.ReadInput(body);

        // Assert
        Assert.Equal([("p1", 3), ("p2", 1), ("p3", 2)], input.Lines.Select(line => (line.ItemId, line.Quantity)));
    }

    [Fact]
    public void ReadInput_ReadsTheLegacyItemsAndCoupons()
    {
        // Arrange
        var body = JsonNode.Parse("""
            { "items": [ { "id": "p1", "quantity": 2 } ], "coupons": [ "WELCOME" ] }
            """).AsObject();

        // Act
        var input = AcpCheckoutMapper.ReadInput(body);

        // Assert
        var line = Assert.Single(input.Lines);

        Assert.Equal("p1", line.ItemId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(["WELCOME"], input.DiscountCodes);
    }

    [Fact]
    public void ReadInput_ReadsTheFulfillmentDetailsAndTheirBuyer()
    {
        // Arrange
        var body = JsonNode.Parse("""
            {
              "line_items": [ { "id": "p1" } ],
              "fulfillment_details": {
                "name": "Ada Lovelace",
                "email": "ada@example.com",
                "phone_number": "+15555550100",
                "address": { "line_one": "1 First St", "city": "Springfield", "state": "IL", "country": "us", "postal_code": "62701" }
              },
              "selected_fulfillment_options": [ { "type": "shipping", "option_id": "express" } ],
              "discounts": { "codes": [ "SAVE10" ] }
            }
            """).AsObject();

        // Act
        var input = AcpCheckoutMapper.ReadInput(body);

        // Assert
        Assert.Equal("Ada Lovelace", input.Fulfillment.Address.Name);
        Assert.Equal("+15555550100", input.Fulfillment.Address.Phone);
        Assert.Equal("US", input.Fulfillment.Address.Country);
        Assert.Equal("express", input.Fulfillment.OptionId);
        Assert.Equal("ada@example.com", input.Buyer.Email);
        Assert.Equal("Ada Lovelace", input.Buyer.FullName);
        Assert.Equal(["SAVE10"], input.DiscountCodes);
    }

    [Fact]
    public void ReadUpdate_WithAnEmptyBody_ChangesNothing()
    {
        // Arrange
        var body = new JsonObject();

        // Act
        var update = AcpCheckoutMapper.ReadUpdate(body, CreateView());

        // Assert
        Assert.Null(update.Lines);
        Assert.Null(update.Buyer);
        Assert.Null(update.Fulfillment);
        Assert.Null(update.DiscountCodes);
        Assert.False(update.ClearFulfillment);
    }

    [Fact]
    public void ReadUpdate_WithNullFulfillmentDetails_ClearsTheFulfillment()
    {
        // Arrange
        var body = JsonNode.Parse("""{ "fulfillment_details": null }""").AsObject();

        // Act
        var update = AcpCheckoutMapper.ReadUpdate(body, CreateView());

        // Assert
        Assert.True(update.ClearFulfillment);
        Assert.Null(update.Fulfillment);
    }

    [Fact]
    public void ReadUpdate_WithOnlyASelectedOption_KeepsTheCurrentAddress()
    {
        // Arrange
        var view = CreateView();
        var body = JsonNode.Parse("""{ "selected_fulfillment_options": [ { "type": "shipping", "option_id": "express" } ] }""").AsObject();

        // Act
        var update = AcpCheckoutMapper.ReadUpdate(body, view);

        // Assert
        Assert.Same(view.Fulfillment.Address, update.Fulfillment.Address);
        Assert.Equal("express", update.Fulfillment.OptionId);
        Assert.False(update.ClearFulfillment);
    }

    [Fact]
    public void ReadUpdate_WithANewAddressAndNoOption_KeepsTheSelectedOption()
    {
        // Arrange
        var body = JsonNode.Parse("""
            { "fulfillment_details": { "address": { "line_one": "9 Ninth St", "country": "US" } } }
            """).AsObject();

        // Act
        var update = AcpCheckoutMapper.ReadUpdate(body, CreateView());

        // Assert
        Assert.Equal("9 Ninth St", update.Fulfillment.Address.AddressLine1);
        Assert.Equal("standard", update.Fulfillment.OptionId);
    }

    [Fact]
    public void ReadUpdate_WithAPickupLocationOption_SelectsThePickup()
    {
        // Arrange
        var view = CreateView();

        view.Fulfillment.Address = null;
        view.Fulfillment.PickupLocations = [new AgentPickupLocationView { Id = "store-1", Name = "Main Street" }];

        var body = JsonNode.Parse("""{ "fulfillment_option_id": "store-1" }""").AsObject();

        // Act
        var update = AcpCheckoutMapper.ReadUpdate(body, view);

        // Assert
        Assert.Equal("store-1", update.Fulfillment.PickupLocationId);
        Assert.Null(update.Fulfillment.Address);
    }

    [Fact]
    public void ReadUpdate_WithCoupons_ReplacesTheCodes()
    {
        // Arrange
        var body = JsonNode.Parse("""{ "coupons": [] }""").AsObject();

        // Act
        var update = AcpCheckoutMapper.ReadUpdate(body, CreateView());

        // Assert
        Assert.NotNull(update.DiscountCodes);
        Assert.Empty(update.DiscountCodes);
    }

    [Fact]
    public void ReadPayment_ReadsTheHandlerShape()
    {
        // Arrange
        var body = JsonNode.Parse("""
            {
              "payment_data": {
                "handler_id": "card_tokenized",
                "instrument": { "type": "card", "credential": { "type": "spt", "token": "spt_123" } },
                "billing_address": { "name": "Ada Lovelace", "line_one": "1 First St", "country": "gb" }
              }
            }
            """).AsObject();

        // Act
        var payment = AcpCheckoutMapper.ReadPayment(body);

        // Assert
        Assert.Equal("card_tokenized", payment.HandlerId);
        Assert.Equal("card", payment.InstrumentType);
        Assert.Equal("spt", payment.CredentialType);
        Assert.Equal("spt_123", payment.Credential);
        Assert.Equal("Ada Lovelace", payment.BillingAddress.Name);
        Assert.Equal("GB", payment.BillingAddress.Country);
    }

    [Fact]
    public void ReadPayment_ReadsTheLegacyStripeToken()
    {
        // Arrange
        var body = JsonNode.Parse("""{ "payment_data": { "token": "spt_legacy", "provider": "stripe" } }""").AsObject();

        // Act
        var payment = AcpCheckoutMapper.ReadPayment(body);

        // Assert
        Assert.Equal(StripeSharedPaymentTokenHandler.AcpName, payment.HandlerId);
        Assert.Equal("dev.acp.tokenized.card", payment.HandlerId);
        Assert.Equal("spt_legacy", payment.Credential);
        Assert.Equal("card", payment.InstrumentType);
    }

    [Fact]
    public void ReadPayment_WithoutPaymentData_ReturnsNull()
    {
        // Act
        var payment = AcpCheckoutMapper.ReadPayment(new JsonObject());

        // Assert
        Assert.Null(payment);
    }

    [Theory]
    [InlineData(AgentCheckoutStatus.Incomplete, "not_ready_for_payment")]
    [InlineData(AgentCheckoutStatus.ReadyForComplete, "ready_for_payment")]
    [InlineData(AgentCheckoutStatus.RequiresEscalation, "requires_escalation")]
    [InlineData(AgentCheckoutStatus.CompleteInProgress, "complete_in_progress")]
    [InlineData(AgentCheckoutStatus.Completed, "completed")]
    [InlineData(AgentCheckoutStatus.Canceled, "canceled")]
    [InlineData(AgentCheckoutStatus.Expired, "expired")]
    public void ToStatus_MapsEveryStatus(AgentCheckoutStatus status, string expected)
    {
        // Act
        var result = AcpCheckoutMapper.ToStatus(status);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task WriteAsync_WritesTotalsWithDisplayTextsAndAPositiveDiscount()
    {
        // Arrange
        var checkouts = new Mock<IAgentCheckoutService>();

        checkouts
            .Setup(service => service.GetPaymentHandlersAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutView>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentPaymentHandlerDescriptor { Id = "card", Name = "dev.acp.tokenized.card", Version = "2026-04-17" }]);

        var mapper = new AcpCheckoutMapper(checkouts.Object);
        var request = new AgentRequest
        {
            Version = AgenticCommerceConstants.Versions.Acp20260417,
            Context = new AgentCheckoutContext { Protocol = AgenticCommerceConstants.Protocols.Acp },
        };
        var view = CreateView();

        view.Totals = new AgentCheckoutTotals
        {
            Subtotal = 100m,
            Discount = 10m,
            Fulfillment = 5m,
            Tax = 7.6m,
            Fee = 1.5m,
            Total = 104.1m,
        };

        // Act
        var json = await mapper.WriteAsync(request, new AgentCheckoutResult { View = view }, TestContext.Current.CancellationToken);

        // Assert
        var totals = json["totals"].AsArray().OfType<JsonObject>().ToList();

        Assert.All(totals, total => Assert.False(string.IsNullOrEmpty(total["display_text"]?.GetValue<string>())));
        Assert.Equal(
            [
                ("items_base_amount", 10000L),
                ("discount", 1000L),
                ("subtotal", 9000L),
                ("fulfillment", 500L),
                ("tax", 760L),
                ("fee", 150L),
                ("total", 10410L),
            ],
            totals.Select(total => (total["type"].GetValue<string>(), total["amount"].GetValue<long>())));
        Assert.Equal("usd", json["currency"].GetValue<string>());
        Assert.Equal("not_ready_for_payment", json["status"].GetValue<string>());
        Assert.Equal("card", json["capabilities"]["payment"]["handlers"][0]["id"].GetValue<string>());
        Assert.Equal("standard", json["selected_fulfillment_options"][0]["option_id"].GetValue<string>());
        Assert.Equal("1 First St", json["fulfillment_details"]["address"]["line_one"].GetValue<string>());
    }

    private static AgentCheckoutView CreateView()
        => new()
        {
            Id = "chk_1",
            Status = AgentCheckoutStatus.Incomplete,
            Currency = "USD",
            Buyer = new AgentBuyer { Email = "ada@example.com", FirstName = "Ada", LastName = "Lovelace" },
            Lines =
            [
                new AgentCheckoutLineView { Id = "line-1", ItemId = "p1", Title = "Widget", Quantity = 1, UnitPrice = 100m, Subtotal = 100m, Total = 100m },
            ],
            Fulfillment = new AgentFulfillmentView
            {
                Required = true,
                Address = new Address { Name = "Ada Lovelace", AddressLine1 = "1 First St", City = "Springfield", Country = "US" },
                SelectedOptionId = "standard",
                Options = [new AgentFulfillmentOptionView { Id = "standard", Title = "Standard", Amount = 5m }],
            },
        };
}
