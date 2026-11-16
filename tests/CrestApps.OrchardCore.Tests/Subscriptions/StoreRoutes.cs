using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.FileProviders;
using Moq;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Environment.Extensions;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Builds the real <see cref="StoreRouteSegments"/> over ASP.NET Core's own <see cref="DefaultEndpointDataSource"/>, with
/// the route patterns a store registers, the module and theme ids Orchard's extension manager lists, and a web root.
/// </summary>
internal static class StoreRoutes
{
    public static StoreRouteSegments Create(params string[] patterns)
        => Create(session: null, patterns);

    public static StoreRouteSegments Create(ISession session, params string[] patterns)
        => Create(session, extensionIds: [], webRoot: null, patterns);

    public static StoreRouteSegments Create(ISession session, string[] extensionIds, IFileProvider webRoot, params string[] patterns)
    {
        var endpoints = patterns
            .Select((pattern, order) => (Endpoint)new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse(pattern),
                order,
                EndpointMetadataCollection.Empty,
                pattern))
            .ToArray();

        var extensionManager = new Mock<IExtensionManager>();
        extensionManager
            .Setup(manager => manager.GetExtensions())
            .Returns(extensionIds.Select(id => Mock.Of<IExtensionInfo>(extension => extension.Id == id)).ToArray());

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(host => host.WebRootFileProvider).Returns(webRoot ?? new NullFileProvider());

        return new StoreRouteSegments(
            new DefaultEndpointDataSource(endpoints),
            session is null ? [] : [Mock.Of<IAutorouteEntries>()],
            session ?? Mock.Of<ISession>(),
            extensionManager.Object,
            environment.Object);
    }
}
