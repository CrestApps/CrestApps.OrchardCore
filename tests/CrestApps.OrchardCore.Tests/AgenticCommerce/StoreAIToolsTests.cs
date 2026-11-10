using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.AI;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// The store's AI tools are marked safe for anonymous storefront visitors, and the cart tools only ever use the cart
/// of the visitor whose request runs them.
/// </summary>
public sealed class StoreAIToolsTests
{
    private static readonly string[] _storeTools =
    [
        SearchStoreProductsTool.ToolName,
        GetStoreProductTool.ToolName,
        StartStoreCheckoutTool.ToolName,
        AddToStoreCartTool.ToolName,
        GetStoreCartTool.ToolName,
    ];

    [Fact]
    public void ConfigureServices_RegistersTheStoreToolsAsSelectableAndMarksThemAnonymousSafe()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new AIToolsStartup(Mock.Of<IStringLocalizer<AIToolsStartup>>()).ConfigureServices(services);

        // Assert
        var provider = services.BuildServiceProvider();
        var selectable = provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.GetSelectableTools();
        var anonymous = provider.GetRequiredService<IOptions<AnonymousAIToolOptions>>().Value;

        Assert.All(_storeTools, name =>
        {
            Assert.True(selectable.ContainsKey(name), name);
            Assert.True(anonymous.IsSafe(name), name);
        });
        Assert.Equal(_storeTools.Length, anonymous.Tools.Count);
    }

    [Fact]
    public async Task GetStoreCart_ReturnsTheCallersOwnCart()
    {
        // Arrange
        var harness = new CartHarness();

        // Act
        var json = await harness.InvokeAsync(new GetStoreCartTool(), new() { ["cartId"] = "someone-elses-cart" });

        // Assert
        var cart = JsonNode.Parse(json);

        Assert.Equal(3, cart["item_count"].GetValue<int>());
        Assert.Equal("USD", cart["currency"].GetValue<string>());
        Assert.Equal(30m, cart["subtotal"].GetValue<decimal>());
        Assert.Equal("item-1", cart["items"][0]["id"].GetValue<string>());
        Assert.Equal("Shirt", cart["items"][0]["title"].GetValue<string>());

        // The tool takes no cart id: it reads the request's cart, and nothing else.
        harness.Carts.Verify(carts => carts.GetCurrentAsync(It.IsAny<CancellationToken>()), Times.Once());
        harness.Carts.Verify(carts => carts.PriceAsync(harness.Cart, It.IsAny<CancellationToken>()), Times.Once());
        harness.Carts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetStoreCart_ReturnsAnEmptyCartWhenTheVisitorHasNone()
    {
        // Arrange
        var harness = new CartHarness { HasCart = false };

        // Act
        var json = await harness.InvokeAsync(new GetStoreCartTool());

        // Assert
        var cart = JsonNode.Parse(json);

        Assert.Equal(0, cart["item_count"].GetValue<int>());
        Assert.Empty(cart["items"].AsArray());
    }

    [Fact]
    public async Task CartTools_RefuseToRunOutsideAVisitorsRequest()
    {
        // Arrange
        var harness = new CartHarness { HasRequest = false };

        // Act
        var view = await harness.InvokeAsync(new GetStoreCartTool());
        var add = await harness.InvokeAsync(new AddToStoreCartTool(), new() { ["id"] = "item-1" });

        // Assert
        Assert.Equal(view, add);
        Assert.DoesNotContain("{", view, StringComparison.Ordinal);
        harness.Carts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddToStoreCart_AddsTheResolvedItemToTheCallersOwnCart()
    {
        // Arrange
        var harness = new CartHarness();

        // Act
        var json = await harness.InvokeAsync(new AddToStoreCartTool(), new() { ["id"] = "item-1", ["quantity"] = 2, ["cartId"] = "someone-elses-cart" });

        // Assert
        var cart = JsonNode.Parse(json);

        Assert.Equal("Shirt was added to your cart.", cart["message"].GetValue<string>());
        Assert.Equal("item-1", cart["items"][0]["id"].GetValue<string>());

        // The cart service finds the caller's cart from the request; the tool only names the item.
        harness.Carts.Verify(carts => carts.AddAsync("product-1", null, "variant-1", 2, It.IsAny<CancellationToken>()), Times.Once());
        harness.Carts.Verify(carts => carts.PriceAsync(harness.Cart, It.IsAny<CancellationToken>()), Times.Once());
        harness.Carts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddToStoreCart_AnswersWhenTheStoreDoesNotSellTheItem()
    {
        // Arrange
        var harness = new CartHarness();

        // Act
        var answer = await harness.InvokeAsync(new AddToStoreCartTool(), new() { ["id"] = "unknown" });

        // Assert
        Assert.Contains("unknown", answer, StringComparison.Ordinal);
        harness.Carts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddToStoreCart_DoesNotStartAGuestCartItCannotGiveTheGuest()
    {
        // Arrange
        var harness = new CartHarness { HasCart = false, ResponseStarted = true };

        // Act
        await harness.InvokeAsync(new AddToStoreCartTool(), new() { ["id"] = "item-1" });

        // Assert
        harness.Carts.Verify(carts => carts.GetCurrentAsync(It.IsAny<CancellationToken>()), Times.Once());
        harness.Carts.VerifyNoOtherCalls();
    }

    private sealed class CartHarness
    {
        public bool HasRequest { get; init; } = true;

        public bool HasCart { get; init; } = true;

        public bool ResponseStarted { get; init; }

        public Mock<ICartService> Carts { get; } = new();

        public Cart Cart { get; } = new()
        {
            ItemId = "cart-1",
            GuestTokenHash = "visitor-hash",
            Currency = "USD",
            Lines =
            [
                new CartLine { LineId = "line-1", ContentItemId = "product-1", VariantId = "variant-1", Quantity = 3 },
            ],
        };

        public async Task<string> InvokeAsync(AIFunction tool, Dictionary<string, object> arguments = null)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()),
            };

            if (ResponseStarted)
            {
                httpContext.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
            }

            Carts
                .Setup(carts => carts.GetCurrentAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(HasCart ? Cart : null);

            Carts
                .Setup(carts => carts.PriceAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Cart cart, CancellationToken _) => new CartPricing
                {
                    Cart = cart,
                    Currency = "USD",
                    Lines = [.. cart.Lines.Select(line => new CartLinePricing { Line = line, Title = "Shirt", UnitPrice = 10m, LineTotal = 10m * line.Quantity })],
                });

            Carts
                .Setup(carts => carts.AddAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CartResult.Success(Cart, new LocalizedString("added", "Shirt was added to your cart.")));

            var resolver = new Mock<IAgentItemResolver>();

            resolver
                .Setup(r => r.ResolveAsync("item-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentItem("item-1", "product-1", "variant-1"));

            resolver
                .Setup(r => r.GetItemId("product-1", "variant-1", It.IsAny<string>()))
                .Returns("item-1");

            var services = new ServiceCollection()
                .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = HasRequest ? httpContext : null })
                .AddSingleton(Carts.Object)
                .AddSingleton(resolver.Object)
                .BuildServiceProvider();

            var functionArguments = new AIFunctionArguments
            {
                Services = services,
            };

            foreach (var (name, value) in arguments ?? [])
            {
                functionArguments[name] = JsonSerializer.SerializeToElement(value);
            }

            return (string)await tool.InvokeAsync(functionArguments, TestContext.Current.CancellationToken);
        }
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
