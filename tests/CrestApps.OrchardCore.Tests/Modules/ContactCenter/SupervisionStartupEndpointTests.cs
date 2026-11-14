using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.ContactCenter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Enabling the supervisor dashboard on a site without the AI feature took the whole tenant down: its AI-calls endpoints
/// take AI services, which weren't registered, so binding them failed while the tenant's pipeline was built. Those
/// endpoints are mapped only with the AI feature now.
/// </summary>
public sealed class SupervisionStartupEndpointTests
{
    [Fact]
    public void WithoutTheAIFeature_TheSupervisionEndpointsBind()
    {
        var services = new WithoutAI(new ServiceCollection().AddLogging().AddRouting().BuildServiceProvider());
        var routes = new TestRouteBuilder(services);

        new SupervisionStartup().Configure(null, routes, services);

        var endpoints = routes.DataSources.SelectMany(source => source.Endpoints).ToList();

        Assert.NotEmpty(endpoints);
        Assert.DoesNotContain(endpoints, endpoint => endpoint.DisplayName?.Contains("ai-calls", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void TheAICallsEndpoints_RequireTheAIFeature()
    {
        var required = typeof(SupervisionAICallsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), false)
            .Cast<RequireFeaturesAttribute>()
            .SelectMany(attribute => attribute.RequiredFeatureNames);

        Assert.Contains(AIConstants.Feature.Area, required);
    }

    // Answers like a tenant without the AI feature: every service is there except the AI ones.
    private sealed class EverythingButAI : IServiceProviderIsService
    {
        public bool IsService(Type serviceType)
            => serviceType != typeof(IAIProfileManager) &&
                serviceType.Namespace?.StartsWith("CrestApps.Core.AI", StringComparison.Ordinal) != true &&
                !serviceType.IsPrimitive &&
                serviceType != typeof(string);
    }

    // The container answers IServiceProviderIsService itself, so the endpoints are bound through this one.
    private sealed class WithoutAI : IServiceProvider
    {
        private readonly IServiceProvider _inner;
        private readonly EverythingButAI _isService = new();

        public WithoutAI(IServiceProvider inner)
            => _inner = inner;

        public object GetService(Type serviceType)
            => serviceType == typeof(IServiceProviderIsService) ? _isService : _inner.GetService(serviceType);
    }

    private sealed class TestRouteBuilder : IEndpointRouteBuilder
    {
        public TestRouteBuilder(IServiceProvider services)
            => ServiceProvider = services;

        public IServiceProvider ServiceProvider { get; }

        public ICollection<EndpointDataSource> DataSources { get; } = [];

        public IApplicationBuilder CreateApplicationBuilder()
            => new ApplicationBuilder(ServiceProvider);
    }
}
