using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C14: an agent can't hoard stock through carts. Carts count against the platform's open-checkout cap, and a cart's
/// id is not accepted where a checkout's is expected, so a cart never becomes a store checkout outside that cap.
/// </summary>
public sealed class AgentCartLimitsTests
{
    private readonly Mock<IAgentCheckoutStore> _store = new();

    [Fact]
    public async Task CreateCart_WhenThePlatformHasTooManyOpen_IsRefused()
    {
        // Arrange
        _store.Setup(store => store.CountOpenAsync("platform-1", It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var services = CreateServices(new AgenticCommerceSettings { MaxOpenCheckoutsPerPlatform = 5 });
        var httpContext = CreateRequest(services, """{ "line_items": [ { "item": { "id": "mug" }, "quantity": 1 } ] }""");

        // Act
        var result = await InvokeEndpointAsync("CreateCartAsync", httpContext, services);

        // Assert
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        _store.Verify(store => store.CreateAsync(It.IsAny<AgentCheckout>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckoutCalls_WithACartsId_FindNoCheckout()
    {
        // Arrange
        var cart = new AgentCheckout { ItemId = "cart-1", PlatformId = "platform-1", Status = AgentCheckoutStatus.Incomplete, Lines = [] };

        cart.SetProperty(UcpCatalogEndpoints.CartKindProperty, "cart");
        _store.Setup(store => store.FindByIdAsync("cart-1", It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        var services = CreateServices(new AgenticCommerceSettings());
        var checkouts = ActivatorUtilities.CreateInstance<AgentCheckoutService>(services);

        // Act
        var result = await checkouts.GetAsync(new AgentCheckoutContext { PlatformId = "platform-1" }, "cart-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentCheckoutFailure.NotFound, result.Failure);
    }

    private ServiceProvider CreateServices(AgenticCommerceSettings settings)
    {
        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();

        site.Put(settings);
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var services = new ServiceCollection()
            .AddSingleton(_store.Object)
            .AddSingleton(siteService.Object)
            .AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance))
            .AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc)))
            .AddSingleton(Mock.Of<global::YesSql.ISession>())
            .AddSingleton(Mock.Of<IAgentCheckoutService>())
            .AddSingleton(Mock.Of<IAgentItemResolver>())
            .AddSingleton(typeof(IStringLocalizer<>), typeof(CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<>))
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Every other dependency of the agent checkout is a loose mock: the calls tested here stop before using them.
        foreach (var parameter in typeof(AgentCheckoutService).GetConstructors()[0].GetParameters())
        {
            if (services.Any(descriptor => descriptor.ServiceType == parameter.ParameterType) ||
                (parameter.ParameterType.IsGenericType && services.Any(descriptor => descriptor.ServiceType == parameter.ParameterType.GetGenericTypeDefinition())))
            {
                continue;
            }

            if (parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                continue;
            }

            if (parameter.ParameterType.IsInterface)
            {
                services.AddSingleton(parameter.ParameterType, ((Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(parameter.ParameterType))).Object);
            }
        }

        // Not reached by the calls tested here.
        services.AddSingleton((AgentSiteUrls)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AgentSiteUrls)));

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateRequest(IServiceProvider services, string body)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var request = new AgentRequest
        {
            Platform = new AgentPlatform { ItemId = "platform-1" },
            Version = "2026-08-25",
            Json = JsonNode.Parse(body),
            Context = new AgentCheckoutContext { PlatformId = "platform-1" },
        };

        request.Set(httpContext);

        return httpContext;
    }

    private static async Task<object> InvokeEndpointAsync(string name, HttpContext httpContext, IServiceProvider services)
    {
        var method = typeof(UcpCatalogEndpoints).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        var arguments = method.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(HttpContext) ? httpContext : services.GetRequiredService(parameter.ParameterType))
            .ToArray();

        var task = (Task)method.Invoke(null, arguments);

        await task;

        return task.GetType().GetProperty("Result").GetValue(task);
    }
}
