using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Orders;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// UCP's A2A binding: the agent card advertises UCP's extension with the offered capabilities, data parts are read into
/// the same requests the REST binding takes, each action runs the matching UCP operation, a message that changes
/// something runs once per messageId, and errors follow UCP's JSON-RPC codes.
/// </summary>
public sealed class UcpA2aTests
{
    private const string Version = "2026-08-25";

    private readonly Mock<IAgentCheckoutService> _checkouts = new();
    private readonly Mock<IOrderStore> _orders = new();
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    public UcpA2aTests()
    {
        _checkouts
            .Setup(service => service.GetPaymentHandlersAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutView>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public void AgentCard_AdvertisesUcpsExtensionPerVersionWithTheOfferedCapabilities()
    {
        // Arrange
        var offers = UcpConstants.Versions.Select(version =>
        {
            var offer = new UcpProfileContext { Version = version, BaseUrl = "https://shop.example" };

            offer.Capabilities.Add(new UcpCapability { Name = UcpConstants.Capabilities.Checkout, Version = version });
            offer.Capabilities.Add(new UcpCapability { Name = UcpConstants.Capabilities.Fulfillment, Version = version, Extends = [UcpConstants.Capabilities.Checkout] });
            offer.Capabilities.Add(new UcpCapability { Name = UcpConstants.Capabilities.Discount, Version = version, Extends = [UcpConstants.Capabilities.Checkout, UcpConstants.Capabilities.Cart] });
            offer.Capabilities.Add(new UcpCapability { Name = UcpConstants.Capabilities.Cart, Version = version });
            offer.Capabilities.Add(new UcpCapability { Name = UcpConstants.Capabilities.CatalogSearch, Version = version });

            return offer;
        });

        // Act
        var card = UcpA2aAgentCard.Build("https://shop.example", "Contoso", offers);

        // Assert
        Assert.Equal("0.3.0", card["protocolVersion"].GetValue<string>());
        Assert.Equal("https://shop.example/ucp/a2a", card["url"].GetValue<string>());
        Assert.Equal("JSONRPC", card["preferredTransport"].GetValue<string>());
        Assert.Equal("Contoso shopping agent", card["name"].GetValue<string>());
        Assert.False(card["capabilities"]["streaming"].GetValue<bool>());

        var extensions = card["capabilities"]["extensions"].AsArray();

        Assert.Equal(UcpConstants.Versions.Count, extensions.Count);
        Assert.Equal("https://ucp.dev/2026-08-25/specification/reference", extensions[0]["uri"].GetValue<string>());
        Assert.Equal("https://ucp.dev/2026-04-08/specification/reference", extensions[1]["uri"].GetValue<string>());

        var capabilities = extensions[0]["params"]["capabilities"].AsObject();

        Assert.Equal(Version, capabilities[UcpConstants.Capabilities.Checkout][0]["version"].GetValue<string>());
        Assert.Equal(UcpConstants.Capabilities.Checkout, capabilities[UcpConstants.Capabilities.Fulfillment][0]["extends"].GetValue<string>());
        Assert.Equal(2, capabilities[UcpConstants.Capabilities.Discount][0]["extends"].AsArray().Count);
        Assert.False(capabilities.ContainsKey(UcpConstants.Capabilities.Cart));
        Assert.False(capabilities.ContainsKey(UcpConstants.Capabilities.CatalogSearch));

        // UCP's schema for the card reads the extensions at the top level too.
        Assert.True(JsonNode.DeepEquals(extensions, card["extensions"]));
        Assert.Contains(card["skills"].AsArray(), skill => skill["id"].GetValue<string>() == "checkout");
        Assert.NotNull(card["securitySchemes"]["apiKey"]);
    }

    [Fact]
    public async Task ProfileContributor_PointsTheA2aServiceAtTheAgentCard()
    {
        // Arrange
        var context = new UcpProfileContext { Version = Version, BaseUrl = "https://shop.example/tenant" };

        // Act
        await new UcpA2aProfileContributor().ContributeAsync(context);

        // Assert
        var service = Assert.Single(context.Services);

        Assert.Equal("a2a", service.Transport);
        Assert.Equal("https://shop.example/tenant/ucp/a2a/.well-known/agent-card.json", service.Endpoint);
        Assert.Null(service.Schema);
    }

    [Fact]
    public void Parse_MergesEveryKindOfDataPartAndKeepsTextApart()
    {
        // Arrange
        var node = JsonNode.Parse("""
            {
              "role": "user",
              "messageId": "m-1",
              "contextId": "c-1",
              "kind": "message",
              "parts": [
                { "kind": "text", "text": "buy it" },
                { "type": "data", "data": { "action": "complete_checkout" } },
                { "kind": "data", "data": { "a2a.ucp.checkout": { "id": "chk_9" } } },
                { "data": { "a2a.ucp.checkout.signals": { "dev.ucp.buyer_ip": "203.0.113.42" } } }
              ]
            }
            """);

        // Act
        var message = UcpA2aMessage.Parse(node);

        // Assert
        Assert.Equal("m-1", message.MessageId);
        Assert.Equal("c-1", message.ContextId);
        Assert.True(message.HasData);
        Assert.Equal("buy it", message.Text);
        Assert.Equal(UcpA2a.Actions.CompleteCheckout, message.Action);
        Assert.Equal("chk_9", message.CheckoutId);
        Assert.Equal("203.0.113.42", message.ToCheckoutBody()["signals"]["dev.ucp.buyer_ip"].GetValue<string>());
    }

    [Fact]
    public void Parse_ATextPartIsNotData()
    {
        // Act
        var message = UcpA2aMessage.Parse(JsonNode.Parse("""{ "messageId": "m-1", "parts": [ { "kind": "text", "text": "hello", "data": { "action": "get_checkout" } } ] }"""));

        // Assert
        Assert.False(message.HasData);
        Assert.Null(message.Action);
        Assert.Null(message.ContextId);
    }

    [Fact]
    public void ToCheckoutBody_WrapsAnInstrumentAndMovesTheAp2Mandate()
    {
        // Arrange
        var message = UcpA2aMessage.Parse(JsonNode.Parse("""
            {
              "messageId": "m-1",
              "parts": [
                { "kind": "data", "data": { "action": "complete_checkout" } },
                {
                  "kind": "data",
                  "data": {
                    "a2a.ucp.checkout.payment": { "id": "instr_1", "handler_id": "gpay", "type": "card", "credential": { "type": "PAYMENT_GATEWAY", "token": "tok" } },
                    "ap2": { "checkout_mandate": "eyJ" }
                  }
                }
              ]
            }
            """));

        // Act
        var body = message.ToCheckoutBody();
        var payment = UcpCheckoutMapper.ReadPayment(body);

        // Assert
        Assert.Equal("gpay", payment.HandlerId);
        Assert.Equal("tok", payment.Credential);
        Assert.Equal("eyJ", body["ap2"]["checkout_mandate"].GetValue<string>());
    }

    [Fact]
    public void ToCheckoutBody_ReadsThePaymentAsSentAndTheSamplesKeys()
    {
        // Arrange
        var message = UcpA2aMessage.Parse(JsonNode.Parse("""
            {
              "messageId": "m-1",
              "parts": [
                {
                  "kind": "data",
                  "data": {
                    "action": "complete_checkout",
                    "a2a.ucp.checkout.payment_data": { "instruments": [ { "handler_id": "h1", "credential": { "token": "t1" } } ] },
                    "a2a.ucp.checkout.risk_signals": { "ip": "203.0.113.5" },
                    "ap2.checkout_mandate": "flat"
                  }
                }
              ]
            }
            """));

        // Act
        var body = message.ToCheckoutBody();

        // Assert
        Assert.Equal("h1", UcpCheckoutMapper.ReadPayment(body).HandlerId);
        Assert.Equal("203.0.113.5", body["signals"]["ip"].GetValue<string>());
        Assert.Equal("flat", body["ap2"]["checkout_mandate"].GetValue<string>());
    }

    [Fact]
    public void Fingerprint_MatchesByMeaningAndTellsMessagesApart()
    {
        // Arrange
        var first = UcpA2aMessage.Parse(JsonNode.Parse("""{ "messageId": "m", "parts": [ { "kind": "data", "data": { "action": "create_checkout", "x": 1 } } ] }"""));
        var reordered = UcpA2aMessage.Parse(JsonNode.Parse("""{ "messageId": "m",   "parts": [ { "data": { "x": 1, "action": "create_checkout" }, "kind": "data" } ] }"""));
        var other = UcpA2aMessage.Parse(JsonNode.Parse("""{ "messageId": "m", "parts": [ { "kind": "data", "data": { "action": "create_checkout", "x": 2 } } ] }"""));

        // Act & Assert
        Assert.Equal(first.GetFingerprint(), reordered.GetFingerprint());
        Assert.NotEqual(first.GetFingerprint(), other.GetFingerprint());
    }

    [Fact]
    public async Task CreateCheckout_RunsTheCreateOperationAndAnswersTheCheckoutInADataPart()
    {
        // Arrange
        AgentCheckoutInput input = null;

        _checkouts
            .Setup(service => service.CreateAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutInput>(), It.IsAny<CancellationToken>()))
            .Callback<AgentCheckoutContext, AgentCheckoutInput, CancellationToken>((_, value, _) => input = value)
            .ReturnsAsync(Opened("chk_1"));

        var handler = CreateHandler();

        // Act
        var answer = await handler.DispatchAsync(CreateRequest(), 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", """{ "action": "create_checkout", "a2a.ucp.checkout": { "line_items": [ { "item": { "id": "p1" }, "quantity": 2 } ] } }"""), TestContext.Current.CancellationToken);

        // Assert
        var line = Assert.Single(input.Lines);

        Assert.Equal("p1", line.ItemId);
        Assert.Equal(2, line.Quantity);

        var result = answer["result"];

        Assert.Equal(1, answer["id"].GetValue<int>());
        Assert.Equal("message", result["kind"].GetValue<string>());
        Assert.Equal("agent", result["role"].GetValue<string>());
        Assert.Equal("c-1", result["contextId"].GetValue<string>());
        Assert.Equal("data", result["parts"][0]["kind"].GetValue<string>());
        Assert.Equal("chk_1", result["parts"][0]["data"][UcpA2a.DataKeys.Checkout]["id"].GetValue<string>());
        Assert.Equal(Version, result["parts"][0]["data"][UcpA2a.DataKeys.Checkout]["ucp"]["version"].GetValue<string>());
    }

    [Fact]
    public async Task ALaterTurnInTheSameContext_ActsOnTheCheckoutTheContextCreated()
    {
        // Arrange
        _checkouts
            .Setup(service => service.CreateAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Opened("chk_1"));
        _checkouts
            .Setup(service => service.CancelAsync(It.IsAny<AgentCheckoutContext>(), "chk_1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Opened("chk_1", AgentCheckoutStatus.Canceled));

        var handler = CreateHandler();
        var request = CreateRequest();

        await handler.DispatchAsync(request, 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", """{ "action": "create_checkout", "a2a.ucp.checkout": { "line_items": [ { "item": { "id": "p1" } } ] } }"""), TestContext.Current.CancellationToken);

        // Act
        var answer = await handler.DispatchAsync(request, 2, UcpA2a.Methods.SendMessage, Send("m-2", "c-1", """{ "action": "cancel_checkout" }"""), TestContext.Current.CancellationToken);

        // Assert
        _checkouts.Verify(service => service.CancelAsync(It.IsAny<AgentCheckoutContext>(), "chk_1", null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("canceled", answer["result"]["parts"][0]["data"][UcpA2a.DataKeys.Checkout]["status"].GetValue<string>());
    }

    [Fact]
    public async Task GetCheckout_WithAnId_ReadsThatCheckout()
    {
        // Arrange
        _checkouts
            .Setup(service => service.GetAsync(It.IsAny<AgentCheckoutContext>(), "chk_7", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Opened("chk_7"));

        // Act
        var answer = await CreateHandler().DispatchAsync(CreateRequest(), "r-1", UcpA2a.Methods.SendMessage, Send("m-1", null, """{ "action": "get_checkout", "id": "chk_7" }"""), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("r-1", answer["id"].GetValue<string>());
        Assert.Equal("chk_7", answer["result"]["parts"][0]["data"][UcpA2a.DataKeys.Checkout]["id"].GetValue<string>());
        Assert.False(string.IsNullOrEmpty(answer["result"]["contextId"].GetValue<string>()));
    }

    [Fact]
    public async Task CompleteCheckout_PaysWithThePaymentDataPart()
    {
        // Arrange
        AgentPayment payment = null;

        _checkouts
            .Setup(service => service.GetAsync(It.IsAny<AgentCheckoutContext>(), "chk_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Opened("chk_1", AgentCheckoutStatus.ReadyForComplete));
        _checkouts
            .Setup(service => service.CompleteAsync(It.IsAny<AgentCheckoutContext>(), "chk_1", It.IsAny<AgentPayment>(), It.IsAny<AgentCheckoutUpdate>(), It.IsAny<CancellationToken>()))
            .Callback<AgentCheckoutContext, string, AgentPayment, AgentCheckoutUpdate, CancellationToken>((_, _, value, _, _) => payment = value)
            .ReturnsAsync(Opened("chk_1", AgentCheckoutStatus.Completed));

        // Act
        var answer = await CreateHandler().DispatchAsync(
            CreateRequest(),
            3,
            UcpA2a.Methods.SendMessage,
            Send("m-3", "c-1", """{ "action": "complete_checkout", "id": "chk_1" }""", """{ "a2a.ucp.checkout.payment": { "handler_id": "dev.crestapps.test", "type": "card", "credential": { "type": "token", "token": "tok_test_success" } } }"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("dev.crestapps.test", payment.HandlerId);
        Assert.Equal("tok_test_success", payment.Credential);
        Assert.Equal("completed", answer["result"]["parts"][0]["data"][UcpA2a.DataKeys.Checkout]["status"].GetValue<string>());
    }

    [Fact]
    public async Task AddToCheckout_RaisesTheQuantityOnTheContextsOpenCheckout()
    {
        // Arrange
        AgentCheckoutUpdate update = null;

        _checkouts
            .Setup(service => service.CreateAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Opened("chk_1"));
        _checkouts
            .Setup(service => service.GetAsync(It.IsAny<AgentCheckoutContext>(), "chk_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Opened("chk_1"));
        _checkouts
            .Setup(service => service.UpdateAsync(It.IsAny<AgentCheckoutContext>(), "chk_1", It.IsAny<AgentCheckoutUpdate>(), It.IsAny<CancellationToken>()))
            .Callback<AgentCheckoutContext, string, AgentCheckoutUpdate, CancellationToken>((_, _, value, _) => update = value)
            .ReturnsAsync(Opened("chk_1"));

        var handler = CreateHandler();
        var request = CreateRequest();

        await handler.DispatchAsync(request, 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", """{ "action": "add_to_checkout", "product_id": "p1" }"""), TestContext.Current.CancellationToken);

        // Act
        await handler.DispatchAsync(request, 2, UcpA2a.Methods.SendMessage, Send("m-2", "c-1", """{ "action": "add_to_checkout", "product_id": "p1", "quantity": 2 }"""), TestContext.Current.CancellationToken);

        // Assert: the open checkout has p1 twice already (see Opened), so it now has four.
        var line = Assert.Single(update.Lines);

        Assert.Equal("p1", line.ItemId);
        Assert.Equal(4, line.Quantity);
    }

    [Fact]
    public void AddLine_AddsANewLineOrRaisesAnExistingOne()
    {
        // Arrange
        var lines = JsonNode.Parse("""[ { "id": "l1", "item": { "id": "p1", "title": "A" }, "quantity": 1 } ]""").AsArray();

        // Act
        var raised = UcpA2aMessageHandler.AddLine(lines, "p1", 2);
        var added = UcpA2aMessageHandler.AddLine(lines, "p2", 1);

        // Assert
        Assert.Equal(3, Assert.Single(raised)["quantity"].GetValue<int>());
        Assert.Equal(["p1", "p2"], added.Select(line => line["item"]["id"].GetValue<string>()));
    }

    [Fact]
    public async Task ARetryOfTheSameMessage_GetsTheFirstAnswerWithoutRunningAgain()
    {
        // Arrange
        _checkouts
            .Setup(service => service.CreateAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Opened("chk_1"));

        var handler = CreateHandler();
        var request = CreateRequest();
        var data = """{ "action": "create_checkout", "a2a.ucp.checkout": { "line_items": [ { "item": { "id": "p1" } } ] } }""";

        var first = await handler.DispatchAsync(request, 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", data), TestContext.Current.CancellationToken);

        // Act
        var retry = await handler.DispatchAsync(request, 2, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", data), TestContext.Current.CancellationToken);

        // Assert
        _checkouts.Verify(service => service.CreateAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutInput>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, retry["id"].GetValue<int>());
        Assert.True(JsonNode.DeepEquals(first["result"], retry["result"]));
    }

    [Fact]
    public async Task TheSameMessageIdFromAnotherPlatform_RunsAgain()
    {
        // Arrange
        _checkouts
            .Setup(service => service.CreateAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Opened("chk_1"));

        var handler = CreateHandler();
        var data = """{ "action": "create_checkout" }""";

        await handler.DispatchAsync(CreateRequest("platform-1"), 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", data), TestContext.Current.CancellationToken);

        // Act
        await handler.DispatchAsync(CreateRequest("platform-2"), 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", data), TestContext.Current.CancellationToken);

        // Assert
        _checkouts.Verify(service => service.CreateAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutInput>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TheSameMessageIdWithAnotherMessage_IsAConflict()
    {
        // Arrange
        _checkouts
            .Setup(service => service.CreateAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Opened("chk_1"));

        var handler = CreateHandler();
        var request = CreateRequest();

        await handler.DispatchAsync(request, 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", """{ "action": "create_checkout", "x": 1 }"""), TestContext.Current.CancellationToken);

        // Act
        var answer = await handler.DispatchAsync(request, 2, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", """{ "action": "create_checkout", "x": 2 }"""), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(UcpJsonRpc.ProtocolError, answer["error"]["code"].GetValue<int>());
        Assert.Equal("idempotency_conflict", answer["error"]["data"]["code"].GetValue<string>());
        _checkouts.Verify(service => service.CreateAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<AgentCheckoutInput>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABusyAnswer_IsNotKeptAndARetryRunsAgain()
    {
        // Arrange
        _checkouts
            .SetupSequence(service => service.CancelAsync(It.IsAny<AgentCheckoutContext>(), "chk_1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentCheckoutResult.Fail(AgentCheckoutFailure.Busy, "Busy."))
            .ReturnsAsync(Opened("chk_1", AgentCheckoutStatus.Canceled));

        var handler = CreateHandler();
        var request = CreateRequest();
        var data = """{ "action": "cancel_checkout", "id": "chk_1" }""";

        var busy = await handler.DispatchAsync(request, 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", data), TestContext.Current.CancellationToken);

        // Act
        var retry = await handler.DispatchAsync(request, 2, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", data), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("conflict", busy["error"]["data"]["code"].GetValue<string>());
        Assert.Equal(1, busy["error"]["data"]["retry_after"].GetValue<int>());
        Assert.Equal("canceled", retry["result"]["parts"][0]["data"][UcpA2a.DataKeys.Checkout]["status"].GetValue<string>());
    }

    [Fact]
    public async Task GetOrder_ForAnOrderThatIsntThePlatforms_IsNotFound()
    {
        // Act
        var answer = await CreateHandler().DispatchAsync(CreateRequest(), 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", """{ "action": "get_order", "id": "order-1" }"""), TestContext.Current.CancellationToken);

        // Assert
        _orders.Verify(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(UcpJsonRpc.ProtocolError, answer["error"]["code"].GetValue<int>());
        Assert.Equal("not_found", answer["error"]["data"]["code"].GetValue<string>());
    }

    [Fact]
    public async Task ACheckoutActionWithoutAnIdOrContext_IsInvalidParams()
    {
        // Act
        var answer = await CreateHandler().DispatchAsync(CreateRequest(), 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-new", """{ "action": "get_checkout" }"""), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(UcpJsonRpc.InvalidParams, answer["error"]["code"].GetValue<int>());
        Assert.Equal("invalid_request", answer["error"]["data"]["code"].GetValue<string>());
    }

    [Theory]
    [InlineData("""{ "action": "buy_everything" }""", UcpJsonRpc.InvalidParams)]
    [InlineData("""{ "a2a.ucp.checkout": { } }""", UcpJsonRpc.InvalidParams)]
    public async Task AnUnknownOrMissingAction_IsInvalidParams(string data, int code)
    {
        // Act
        var answer = await CreateHandler().DispatchAsync(CreateRequest(), 1, UcpA2a.Methods.SendMessage, Send("m-1", "c-1", data), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(code, answer["error"]["code"].GetValue<int>());
    }

    [Fact]
    public async Task AMessageWithoutAMessageId_IsInvalidParams()
    {
        // Arrange
        var parameters = JsonNode.Parse("""{ "message": { "role": "user", "parts": [ { "kind": "data", "data": { "action": "create_checkout" } } ] } }""").AsObject();

        // Act
        var answer = await CreateHandler().DispatchAsync(CreateRequest(), 1, UcpA2a.Methods.SendMessage, parameters, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(UcpJsonRpc.InvalidParams, answer["error"]["code"].GetValue<int>());
    }

    [Fact]
    public async Task ATextOnlyMessage_IsAnsweredWithTheActionsItTakes()
    {
        // Arrange
        var parameters = JsonNode.Parse("""{ "message": { "role": "user", "messageId": "m-1", "contextId": "c-1", "parts": [ { "kind": "text", "text": "add a Pixel to my checkout" } ] } }""").AsObject();

        // Act
        var answer = await CreateHandler().DispatchAsync(CreateRequest(), 1, UcpA2a.Methods.SendMessage, parameters, TestContext.Current.CancellationToken);

        // Assert
        var part = answer["result"]["parts"][0];

        Assert.Equal("text", part["kind"].GetValue<string>());
        Assert.Contains(UcpA2a.Actions.CreateCheckout, part["text"].GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("c-1", answer["result"]["contextId"].GetValue<string>());
    }

    [Theory]
    [InlineData("message/stream", UcpA2a.ErrorCodes.UnsupportedOperation)]
    [InlineData("tasks/get", UcpA2a.ErrorCodes.TaskNotFound)]
    [InlineData("tasks/cancel", UcpA2a.ErrorCodes.TaskNotFound)]
    [InlineData("tasks/pushNotificationConfig/set", UcpA2a.ErrorCodes.PushNotificationNotSupported)]
    [InlineData("tools/list", UcpJsonRpc.MethodNotFound)]
    public async Task OtherMethods_GetA2asErrors(string method, int code)
    {
        // Act
        var answer = await CreateHandler().DispatchAsync(CreateRequest(), 9, method, [], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(9, answer["id"].GetValue<int>());
        Assert.Equal(code, answer["error"]["code"].GetValue<int>());
    }

    [Theory]
    [InlineData("invalid_profile_url", UcpJsonRpc.DiscoveryError)]
    [InlineData("profile_unreachable", UcpJsonRpc.DiscoveryError)]
    [InlineData("profile_malformed", UcpJsonRpc.DiscoveryError)]
    [InlineData("version_unsupported", UcpJsonRpc.DiscoveryError)]
    [InlineData("unauthorized", UcpJsonRpc.ProtocolError)]
    [InlineData("forbidden", UcpJsonRpc.ProtocolError)]
    [InlineData("signature_invalid", UcpJsonRpc.ProtocolError)]
    [InlineData("idempotency_conflict", UcpJsonRpc.ProtocolError)]
    [InlineData("not_found", UcpJsonRpc.ProtocolError)]
    [InlineData("rate_limited", UcpJsonRpc.ProtocolError)]
    [InlineData("digest_mismatch", UcpJsonRpc.InvalidRequest)]
    [InlineData("algorithm_unsupported", UcpJsonRpc.InvalidRequest)]
    [InlineData("invalid_request", UcpJsonRpc.InvalidParams)]
    public void ToErrorCode_FollowsUcpsJsonRpcCodes(string ucpCode, int expected)
        => Assert.Equal(expected, UcpJsonRpc.ToErrorCode(ucpCode));

    [Fact]
    public async Task RequestFilter_OnTheA2aEndpoint_AnswersAJsonRpcErrorWithTheRestStatus()
    {
        // Arrange
        var httpContext = FilterRequest(withJsonRpcErrors: true);

        // Act
        var result = await new UcpRequestFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult<object>(null));

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);

        var body = Assert.IsType<JsonObject>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);

        Assert.Equal("2.0", body["jsonrpc"].GetValue<string>());
        Assert.Equal(7, body["id"].GetValue<int>());
        Assert.Equal(UcpJsonRpc.DiscoveryError, body["error"]["code"].GetValue<int>());
        Assert.Equal("invalid_profile_url", body["error"]["data"]["code"].GetValue<string>());
    }

    [Fact]
    public async Task RequestFilter_OnARestEndpoint_KeepsTheRestError()
    {
        // Arrange
        var httpContext = FilterRequest(withJsonRpcErrors: false);

        // Act
        var result = await new UcpRequestFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult<object>(null));

        // Assert
        var body = Assert.IsType<JsonObject>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);

        Assert.Equal("invalid_profile_url", body["code"].GetValue<string>());
        Assert.Null(body["jsonrpc"]);
    }

    [Theory]
    [InlineData("https://ucp.dev/2026-08-25/specification/reference", true)]
    [InlineData("https://example.com/other, https://ucp.dev/2026-04-08/specification/reference", true)]
    [InlineData("https://ucp.dev/2026-01-11/specification/reference", false)]
    [InlineData("", false)]
    public void IsExtensionRequested_MatchesAServedVersionsUri(string header, bool expected)
        => Assert.Equal(expected, UcpA2aMessageHandler.IsExtensionRequested(header));

    private static DefaultHttpContext FilterRequest(bool withJsonRpcErrors)
    {
        var httpContext = new DefaultHttpContext();
        var body = """{ "jsonrpc": "2.0", "id": 7, "method": "message/send" }"""u8.ToArray();

        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Body = new MemoryStream(body);
        httpContext.Request.ContentLength = body.Length;
        httpContext.Request.Headers[UcpConstants.AgentHeader] = "not a dictionary";
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            withJsonRpcErrors ? new EndpointMetadataCollection(UcpJsonRpc.Errors) : new EndpointMetadataCollection(),
            "test"));

        return httpContext;
    }

    private UcpA2aMessageHandler CreateHandler()
    {
        var mapper = new UcpCheckoutMapper(_checkouts.Object, []);
        var operations = new UcpOperations(
            _checkouts.Object,
            mapper,
            [],
            Mock.Of<IAgentCheckoutStore>(),
            _orders.Object,
            new AgentOrderViewBuilder(Mock.Of<IAgentItemResolver>(), [], [], null));

        return new UcpA2aMessageHandler(operations, _cache, new LocalLock(NullLogger<LocalLock>.Instance), shellSettings: null);
    }

    private static AgentRequest CreateRequest(string platformId = "platform-1")
    {
        var request = new AgentRequest
        {
            Platform = new AgentPlatform { ItemId = platformId },
            Version = Version,
            Context = new AgentCheckoutContext
            {
                PlatformId = platformId,
                Protocol = AgenticCommerceConstants.Protocols.Ucp,
                ProtocolVersion = Version,
            },
        };

        request.Capabilities[UcpConstants.Capabilities.Checkout] = Version;

        return request;
    }

    private static JsonObject Send(string messageId, string contextId, params string[] dataParts)
    {
        var parts = new JsonArray();

        foreach (var data in dataParts)
        {
            parts.Add(new JsonObject { ["kind"] = "data", ["data"] = JsonNode.Parse(data) });
        }

        var message = new JsonObject
        {
            ["role"] = "user",
            ["kind"] = "message",
            ["messageId"] = messageId,
            ["parts"] = parts,
        };

        if (contextId is not null)
        {
            message["contextId"] = contextId;
        }

        return new JsonObject { ["message"] = message };
    }

    private static AgentCheckoutResult Opened(string id, AgentCheckoutStatus status = AgentCheckoutStatus.Incomplete)
        => new()
        {
            Checkout = new AgentCheckout { ItemId = id, PlatformId = "platform-1" },
            View = new AgentCheckoutView
            {
                Id = id,
                Status = status,
                Currency = "usd",
                Lines =
                [
                    new AgentCheckoutLineView { Id = "line-1", ItemId = "p1", Title = "Widget", Quantity = 2, UnitPrice = 10m, Subtotal = 20m, Total = 20m },
                ],
                Totals = new AgentCheckoutTotals { Subtotal = 20m, Total = 20m },
            },
        };
}
