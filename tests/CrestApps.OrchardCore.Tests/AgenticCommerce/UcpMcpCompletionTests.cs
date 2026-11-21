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
using Moq;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C8: UCP's MCP <c>complete_checkout</c> runs the checkout extensions as REST and A2A do, so a mandate-verified
/// completion keeps the AP2 mandate and the store's receipt whichever binding paid it.
/// </summary>
public sealed class UcpMcpCompletionTests
{
    private const string Version = "2026-08-25";

    [Fact]
    public async Task CompleteCheckout_RunsTheCheckoutExtensions_AndTheirCompletedStepOnceCompleted()
    {
        // Arrange
        var checkouts = new Mock<IAgentCheckoutService>();
        var extension = new RecordingExtension();
        var mapper = new UcpCheckoutMapper(checkouts.Object, [extension]);
        var operations = new UcpOperations(checkouts.Object, mapper, [extension], Mock.Of<IAgentCheckoutStore>(), Mock.Of<IOrderStore>(), new AgentOrderViewBuilder(Mock.Of<IAgentItemResolver>(), [], [], null));

        checkouts.Setup(service => service.GetAsync(It.IsAny<AgentCheckoutContext>(), "chk_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(AgentCheckoutStatus.ReadyForComplete));
        checkouts.Setup(service => service.CompleteAsync(It.IsAny<AgentCheckoutContext>(), "chk_1", It.IsAny<AgentPayment>(), It.IsAny<AgentCheckoutUpdate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(AgentCheckoutStatus.Completed));

        var httpContext = new DefaultHttpContext();

        CreateRequest(JsonNode.Parse("""
            {
              "jsonrpc": "2.0",
              "id": 1,
              "method": "tools/call",
              "params": {
                "name": "complete_checkout",
                "arguments": {
                  "id": "chk_1",
                  "checkout": { "payment": { "instruments": [ { "handler_id": "card", "type": "card", "credential": { "type": "token", "token": "tok" } } ] } }
                }
              }
            }
            """)).Set(httpContext);

        // Act
        await UcpMcpEndpoints.HandleAsync(httpContext, checkouts.Object, mapper, operations);

        // Assert
        Assert.Equal(1, extension.Completing);
        Assert.Equal(1, extension.Completed);
    }

    private static AgentRequest CreateRequest(JsonNode json)
    {
        var request = new AgentRequest
        {
            Platform = new AgentPlatform { ItemId = "platform-1" },
            Version = Version,
            Json = json,
            Context = new AgentCheckoutContext
            {
                PlatformId = "platform-1",
                Protocol = AgenticCommerceConstants.Protocols.Ucp,
                ProtocolVersion = Version,
            },
        };

        request.Capabilities[UcpConstants.Capabilities.Checkout] = Version;

        return request;
    }

    private static AgentCheckoutResult Result(AgentCheckoutStatus status)
        => new()
        {
            Checkout = new AgentCheckout { ItemId = "chk_1", PlatformId = "platform-1" },
            View = new AgentCheckoutView
            {
                Id = "chk_1",
                Status = status,
                Currency = "usd",
                Lines = [new AgentCheckoutLineView { Id = "line-1", ItemId = "p1", Title = "Widget", Quantity = 1, UnitPrice = 10m, Subtotal = 10m, Total = 10m }],
                Totals = new AgentCheckoutTotals { Subtotal = 10m, Total = 10m },
            },
        };

    private sealed class RecordingExtension : IUcpCheckoutExtension
    {
        public int Completing { get; private set; }

        public int Completed { get; private set; }

        public Task WritingAsync(UcpCheckoutContext context)
            => Task.CompletedTask;

        public Task<AgentCheckoutMessage> CompletingAsync(UcpCheckoutContext context)
        {
            Completing++;

            return Task.FromResult<AgentCheckoutMessage>(null);
        }

        public Task CompletedAsync(UcpCheckoutContext context)
        {
            Completed++;

            return Task.CompletedTask;
        }
    }
}
