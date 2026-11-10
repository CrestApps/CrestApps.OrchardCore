using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Moq;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// UCP checkout JSON is read into the agent checkout's input and its view is written back in the agreed version's
/// shape, with totals whose parts add up to the total and only the checkout capabilities echoed.
/// </summary>
public sealed class UcpCheckoutMapperTests
{
    [Fact]
    public void ReadInput_ReadsLinesBuyerFulfillmentDiscountsAndConsents()
    {
        // Arrange
        var body = JsonNode.Parse("""
            {
              "line_items": [ { "item": { "id": "p1" }, "quantity": 2 }, { "item": { "id": "p2" } } ],
              "buyer": {
                "first_name": "Ada",
                "last_name": "Lovelace",
                "email": "ada@example.com",
                "consent": { "marketing": { "granted": true }, "analytics": false }
              },
              "fulfillment": {
                "methods": [
                  {
                    "type": "shipping",
                    "selected_destination_id": "d2",
                    "destinations": [
                      { "id": "d1", "street_address": "1 First St", "address_country": "us" },
                      { "id": "d2", "street_address": "2 Second St", "address_locality": "Springfield", "address_country": "us" }
                    ],
                    "groups": [ { "id": "g1", "selected_option_id": "express" } ]
                  }
                ]
              },
              "discounts": { "codes": [ "SAVE10" ] },
              "signals": { "ip": "203.0.113.5" }
            }
            """).AsObject();

        // Act
        var input = UcpCheckoutMapper.ReadInput(body);

        // Assert
        Assert.Collection(
            input.Lines,
            line =>
            {
                Assert.Equal("p1", line.ItemId);
                Assert.Equal(2, line.Quantity);
            },
            line =>
            {
                Assert.Equal("p2", line.ItemId);
                Assert.Equal(1, line.Quantity);
            });
        Assert.Equal("ada@example.com", input.Buyer.Email);
        Assert.Equal("Ada", input.Buyer.FirstName);
        Assert.Equal("2 Second St", input.Fulfillment.Address.AddressLine1);
        Assert.Equal("Springfield", input.Fulfillment.Address.City);
        Assert.Equal("US", input.Fulfillment.Address.Country);
        Assert.Equal("express", input.Fulfillment.OptionId);
        Assert.Equal(["SAVE10"], input.DiscountCodes);
        Assert.Equal("203.0.113.5", input.Signals["ip"]);
        Assert.True(input.Consents["marketing"]);
        Assert.False(input.Consents["analytics"]);
    }

    [Fact]
    public void ReadInput_ForAPickupMethod_ReadsTheSelectedLocation()
    {
        // Arrange
        var body = JsonNode.Parse("""
            { "line_items": [], "fulfillment": { "methods": [ { "type": "pickup", "selected_destination_id": "store-1" } ] } }
            """).AsObject();

        // Act
        var input = UcpCheckoutMapper.ReadInput(body);

        // Assert
        Assert.Equal("store-1", input.Fulfillment.PickupLocationId);
        Assert.Null(input.Fulfillment.Address);
    }

    [Fact]
    public void ReadUpdate_LeavesOutWhatTheBodyLeavesOut()
    {
        // Arrange
        var body = JsonNode.Parse("""{ "buyer": { "email": "ada@example.com" } }""").AsObject();

        // Act
        var update = UcpCheckoutMapper.ReadUpdate(body);

        // Assert
        Assert.Equal("ada@example.com", update.Buyer.Email);
        Assert.NotNull(update.Consents);
        Assert.Null(update.Lines);
        Assert.Null(update.Fulfillment);
        Assert.Null(update.DiscountCodes);
        Assert.Null(update.Signals);
        Assert.Null(update.Attribution);
        Assert.False(update.ClearFulfillment);
    }

    [Fact]
    public void ReadUpdate_WithEmptyDiscounts_ClearsTheCodes()
    {
        // Arrange
        var body = JsonNode.Parse("""{ "discounts": {} }""").AsObject();

        // Act
        var update = UcpCheckoutMapper.ReadUpdate(body);

        // Assert
        Assert.NotNull(update.DiscountCodes);
        Assert.Empty(update.DiscountCodes);
    }

    [Fact]
    public void ReadPayment_TakesTheSelectedInstrument()
    {
        // Arrange
        var body = JsonNode.Parse("""
            {
              "payment": {
                "instruments": [
                  { "handler_id": "h1", "type": "card", "credential": { "type": "token", "token": "tok_first" } },
                  {
                    "handler_id": "h2",
                    "type": "card",
                    "selected": true,
                    "credential": { "type": "token", "token": "tok_second" },
                    "display": { "brand": "visa", "last_digits": "4242" },
                    "billing_address": { "full_name": "Ada Lovelace", "street_address": "1 First St", "address_country": "gb" }
                  }
                ]
              }
            }
            """).AsObject();

        // Act
        var payment = UcpCheckoutMapper.ReadPayment(body);

        // Assert
        Assert.Equal("h2", payment.HandlerId);
        Assert.Equal("card", payment.InstrumentType);
        Assert.Equal("token", payment.CredentialType);
        Assert.Equal("tok_second", payment.Credential);
        Assert.Equal("visa", payment.Brand);
        Assert.Equal("4242", payment.LastDigits);
        Assert.Equal("Ada Lovelace", payment.BillingAddress.Name);
        Assert.Equal("GB", payment.BillingAddress.Country);
    }

    [Fact]
    public void ReadPayment_WithoutASelectedInstrument_TakesTheFirst()
    {
        // Arrange
        var body = JsonNode.Parse("""
            { "payment": { "instruments": [ { "handler_id": "h1" }, { "handler_id": "h2" } ] } }
            """).AsObject();

        // Act
        var payment = UcpCheckoutMapper.ReadPayment(body);

        // Assert
        Assert.Equal("h1", payment.HandlerId);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "payment": { "instruments": [] } }""")]
    public void ReadPayment_WithoutInstruments_ReturnsNull(string json)
    {
        // Arrange
        var body = JsonNode.Parse(json).AsObject();

        // Act
        var payment = UcpCheckoutMapper.ReadPayment(body);

        // Assert
        Assert.Null(payment);
    }

    [Fact]
    public void WriteAddress_ThenReadAddress_RoundTrips()
    {
        // Arrange
        var address = new Address
        {
            Name = "Ada King Lovelace",
            AddressLine1 = "1 First St",
            AddressLine2 = "Flat 2",
            City = "London",
            Region = "LND",
            PostalCode = "N1 1AA",
            Country = "GB",
            Phone = "+442000000000",
        };

        // Act
        var json = UcpCheckoutMapper.WriteAddress(address);
        var read = UcpCheckoutMapper.ReadAddress(json);

        // Assert
        Assert.Equal("Ada", json["first_name"].GetValue<string>());
        Assert.Equal("King Lovelace", json["last_name"].GetValue<string>());
        Assert.Equal(address.Name, read.Name);
        Assert.Equal(address.AddressLine1, read.AddressLine1);
        Assert.Equal(address.AddressLine2, read.AddressLine2);
        Assert.Equal(address.City, read.City);
        Assert.Equal(address.Region, read.Region);
        Assert.Equal(address.PostalCode, read.PostalCode);
        Assert.Equal(address.Country, read.Country);
        Assert.Equal(address.Phone, read.Phone);
    }

    [Theory]
    [InlineData(AgentCheckoutStatus.Incomplete, "incomplete")]
    [InlineData(AgentCheckoutStatus.RequiresEscalation, "requires_escalation")]
    [InlineData(AgentCheckoutStatus.ReadyForComplete, "ready_for_complete")]
    [InlineData(AgentCheckoutStatus.CompleteInProgress, "complete_in_progress")]
    [InlineData(AgentCheckoutStatus.Completed, "completed")]
    [InlineData(AgentCheckoutStatus.Canceled, "canceled")]
    [InlineData(AgentCheckoutStatus.Expired, "canceled")]
    public void ToStatus_MapsEveryStatus(AgentCheckoutStatus status, string expected)
    {
        // Act
        var result = UcpCheckoutMapper.ToStatus(status);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WriteTotals_PartsAddUpToTheTotal_WithANegativeDiscount()
    {
        // Arrange
        var totals = new AgentCheckoutTotals
        {
            Subtotal = 100m,
            Discount = 10m,
            Fulfillment = 5m,
            Tax = 7.6m,
            Fee = 1.5m,
            Total = 104.1m,
        };

        // Act
        var json = UcpCheckoutMapper.WriteTotals(totals, "USD");

        // Assert
        var amounts = json.OfType<JsonObject>().ToDictionary(entry => entry["type"].GetValue<string>(), entry => entry["amount"].GetValue<long>());

        Assert.Equal(["subtotal", "discount", "fulfillment", "tax", "fee", "total"], amounts.Keys);
        Assert.Equal(-1000, amounts["discount"]);
        Assert.Equal(amounts["total"], amounts.Where(entry => entry.Key != "total").Sum(entry => entry.Value));
    }

    [Fact]
    public void WriteTotals_LeavesOutZeroParts()
    {
        // Arrange
        var totals = new AgentCheckoutTotals { Subtotal = 20m, Total = 20m };

        // Act
        var json = UcpCheckoutMapper.WriteTotals(totals, "USD");

        // Assert
        Assert.Equal(["subtotal", "total"], json.Select(entry => entry["type"].GetValue<string>()));
    }

    [Theory]
    [InlineData(AgentCheckoutFields.BuyerEmail, "$.buyer.email")]
    [InlineData(AgentCheckoutFields.FulfillmentAddress, "$.fulfillment.methods[0].destinations[0]")]
    [InlineData(AgentCheckoutFields.FulfillmentOption, "$.fulfillment.methods[0].groups[0].selected_option_id")]
    [InlineData(AgentCheckoutFields.BillingAddress, "$.payment.instruments[0].billing_address")]
    [InlineData("line_items[1]", "$.line_items[1]")]
    [InlineData(null, null)]
    public void ToPath_MapsFieldsToJsonPaths(string field, string expected)
    {
        // Act
        var path = UcpCheckoutMapper.ToPath(field);

        // Assert
        Assert.Equal(expected, path);
    }

    [Fact]
    public void WriteMessage_ForAnError_WritesItsSeverityAndPath()
    {
        // Arrange
        var message = AgentCheckoutMessage.Error(AgentMessageCodes.Missing, AgentCheckoutFields.BuyerEmail, "Email is required.", AgentMessageSeverity.RequiresBuyerInput);

        // Act
        var json = UcpCheckoutMapper.WriteMessage(message);

        // Assert
        Assert.Equal("error", json["type"].GetValue<string>());
        Assert.Equal("missing", json["code"].GetValue<string>());
        Assert.Equal("$.buyer.email", json["path"].GetValue<string>());
        Assert.Equal("requires_buyer_input", json["severity"].GetValue<string>());
    }

    [Fact]
    public void WriteMessage_ForAWarning_HasNoSeverityOrPathWithoutAField()
    {
        // Arrange
        var message = AgentCheckoutMessage.Warning(AgentMessageCodes.PriceChanged, null, "The price changed.");

        // Act
        var json = UcpCheckoutMapper.WriteMessage(message);

        // Assert
        Assert.Equal("warning", json["type"].GetValue<string>());
        Assert.False(json.ContainsKey("severity"));
        Assert.False(json.ContainsKey("path"));
    }

    [Theory]
    [InlineData(AgenticCommerceConstants.Versions.Ucp20260408, false)]
    [InlineData(AgenticCommerceConstants.Versions.Ucp20260825, true)]
    public async Task WriteAsync_WritesTheOptionDescriptionInTheVersionsShape(string version, bool rich)
    {
        // Arrange
        var mapper = CreateMapper();
        var request = CreateRequest(version, UcpConstants.Capabilities.Checkout, UcpConstants.Capabilities.Fulfillment);
        var view = CreateView();

        view.Fulfillment = new AgentFulfillmentView
        {
            Required = true,
            Address = new Address { Name = "Ada Lovelace", AddressLine1 = "1 First St", Country = "US" },
            SelectedOptionId = "standard",
            Options =
            [
                new AgentFulfillmentOptionView { Id = "standard", Title = "Standard", Description = "3 to 5 days", Amount = 5m },
            ],
        };

        // Act
        var json = await mapper.WriteAsync(request, new AgentCheckoutResult { View = view }, TestContext.Current.CancellationToken);

        // Assert
        var group = json["fulfillment"]["methods"][0]["groups"][0];
        var description = group["options"][0]["description"];

        Assert.Equal("standard", group["selected_option_id"].GetValue<string>());

        if (rich)
        {
            Assert.Equal("3 to 5 days", description["plain"].GetValue<string>());
        }
        else
        {
            Assert.Equal("3 to 5 days", description.GetValue<string>());
        }
    }

    [Fact]
    public async Task WriteAsync_EchoesOnlyTheCheckoutCapabilities()
    {
        // Arrange
        var mapper = CreateMapper();
        var request = CreateRequest(
            AgenticCommerceConstants.Versions.Ucp20260825,
            UcpConstants.Capabilities.Checkout,
            UcpConstants.Capabilities.Discount,
            UcpConstants.Capabilities.Ap2Mandate,
            UcpConstants.Capabilities.Order,
            UcpConstants.Capabilities.CatalogSearch);

        // Act
        var json = await mapper.WriteAsync(request, new AgentCheckoutResult { View = CreateView() }, TestContext.Current.CancellationToken);

        // Assert
        var capabilities = json["ucp"]["capabilities"].AsObject();

        Assert.Equal(
            [UcpConstants.Capabilities.Checkout, UcpConstants.Capabilities.Discount, UcpConstants.Capabilities.Ap2Mandate],
            capabilities.Select(entry => entry.Key));
        Assert.Equal(AgenticCommerceConstants.Versions.Ucp20260825, capabilities[UcpConstants.Capabilities.Checkout][0]["version"].GetValue<string>());
        Assert.Equal("v1", json["ucp"]["payment_handlers"]["com.example.pay"][0]["version"].GetValue<string>());
        Assert.False(json["ucp"].AsObject().ContainsKey("status"));
        Assert.NotNull(json["discounts"]);
    }

    [Fact]
    public async Task WriteAsync_ForACompletedCheckout_WritesTheOrder()
    {
        // Arrange
        var mapper = CreateMapper();
        var request = CreateRequest(AgenticCommerceConstants.Versions.Ucp20260825, UcpConstants.Capabilities.Checkout);
        var view = CreateView();

        view.Status = AgentCheckoutStatus.Completed;
        view.Order = new AgentOrderReference { Id = "order-1", Number = "1001", PermalinkUrl = "https://store.example.com/orders/1001" };

        // Act
        var json = await mapper.WriteAsync(request, new AgentCheckoutResult { View = view }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("completed", json["status"].GetValue<string>());
        Assert.Equal("order-1", json["order"]["id"].GetValue<string>());
        Assert.Equal("1001", json["order"]["label"].GetValue<string>());
        Assert.Equal("https://store.example.com/orders/1001", json["order"]["permalink_url"].GetValue<string>());
    }

    [Fact]
    public async Task WriteAsync_ForAnOpenCheckout_HasNoOrder()
    {
        // Arrange
        var mapper = CreateMapper();
        var request = CreateRequest(AgenticCommerceConstants.Versions.Ucp20260825, UcpConstants.Capabilities.Checkout);

        // Act
        var json = await mapper.WriteAsync(request, new AgentCheckoutResult { View = CreateView() }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(json.ContainsKey("order"));
        Assert.False(json.ContainsKey("fulfillment"));
        Assert.False(json.ContainsKey("discounts"));
    }

    [Fact]
    public async Task WriteAsync_MapsTheReturnPolicyToTheRefundPolicyAndDropsUnknownLinks()
    {
        // Arrange
        var mapper = CreateMapper();
        var request = CreateRequest(AgenticCommerceConstants.Versions.Ucp20260825, UcpConstants.Capabilities.Checkout);
        var view = CreateView();

        view.Links =
        [
            new AgentLink { Type = AgentLinkTypes.ReturnPolicy, Url = "https://store.example.com/returns" },
            new AgentLink { Type = AgentLinkTypes.TermsOfService, Url = "https://store.example.com/terms" },
            new AgentLink { Type = AgentLinkTypes.Contact, Url = "https://store.example.com/contact" },
        ];

        // Act
        var json = await mapper.WriteAsync(request, new AgentCheckoutResult { View = view }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Collection(
            json["links"].AsArray(),
            link =>
            {
                Assert.Equal("refund_policy", link["type"].GetValue<string>());
                Assert.Equal("https://store.example.com/returns", link["url"].GetValue<string>());
            },
            link => Assert.Equal("terms_of_service", link["type"].GetValue<string>()));
    }

    [Fact]
    public async Task WriteAsync_ForAFailedResult_MarksTheAnswerAsAnError()
    {
        // Arrange
        var mapper = CreateMapper();
        var request = CreateRequest(AgenticCommerceConstants.Versions.Ucp20260825, UcpConstants.Capabilities.Checkout);
        var result = AgentCheckoutResult.Fail(AgentCheckoutFailure.InvalidState, "The checkout was already completed.", view: CreateView());

        // Act
        var json = await mapper.WriteAsync(request, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("error", json["ucp"]["status"].GetValue<string>());

        var message = Assert.Single(json["messages"].AsArray());

        Assert.Equal(AgentMessageCodes.InvalidState, message["code"].GetValue<string>());
        Assert.Equal("unrecoverable", message["severity"].GetValue<string>());
        Assert.Equal("The checkout was already completed.", message["content"].GetValue<string>());
    }

    [Fact]
    public async Task WriteAsync_WritesLineTotalsAndTheCurrencyInUpperCase()
    {
        // Arrange
        var mapper = CreateMapper();
        var request = CreateRequest(AgenticCommerceConstants.Versions.Ucp20260825, UcpConstants.Capabilities.Checkout);

        // Act
        var json = await mapper.WriteAsync(request, new AgentCheckoutResult { View = CreateView() }, TestContext.Current.CancellationToken);

        // Assert
        var line = Assert.Single(json["line_items"].AsArray());

        Assert.Equal("USD", json["currency"].GetValue<string>());
        Assert.Equal("p1", line["item"]["id"].GetValue<string>());
        Assert.Equal(1000, line["item"]["price"].GetValue<long>());
        Assert.Equal(
            [("subtotal", 2000L), ("discount", -200L), ("total", 1800L)],
            line["totals"].AsArray().Select(total => (total["type"].GetValue<string>(), total["amount"].GetValue<long>())));
    }

    private static UcpCheckoutMapper CreateMapper()
    {
        var checkouts = new Mock<IAgentCheckoutService>();

        checkouts
            .Setup(service => service.GetPaymentHandlersAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutView>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AgentPaymentHandlerDescriptor { Id = "pay-1", Name = "com.example.pay", Version = "v1", InstrumentTypes = ["card"] },
            ]);

        return new UcpCheckoutMapper(checkouts.Object, []);
    }

    private static AgentRequest CreateRequest(string version, params string[] capabilities)
    {
        var request = new AgentRequest
        {
            Version = version,
            Context = new AgentCheckoutContext { Protocol = AgenticCommerceConstants.Protocols.Ucp, ProtocolVersion = version },
        };

        foreach (var capability in capabilities)
        {
            request.Capabilities[capability] = version;
        }

        return request;
    }

    private static AgentCheckoutView CreateView()
        => new()
        {
            Id = "chk_1",
            Status = AgentCheckoutStatus.Incomplete,
            Currency = "usd",
            Lines =
            [
                new AgentCheckoutLineView { Id = "line-1", ItemId = "p1", Title = "Widget", Quantity = 2, UnitPrice = 10m, Subtotal = 20m, Discount = 2m, Total = 18m },
            ],
            Totals = new AgentCheckoutTotals { Subtotal = 20m, Discount = 2m, Total = 18m },
        };
}
