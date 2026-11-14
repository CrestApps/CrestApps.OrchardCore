using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Moq;
using OrchardCore.ContentManagement.Routing;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Builds the real <see cref="StoreRouteSegments"/> over ASP.NET Core's own <see cref="DefaultEndpointDataSource"/>, with
/// the route patterns a store registers.
/// </summary>
internal static class StoreRoutes
{
    public static StoreRouteSegments Create(params string[] patterns)
        => Create(session: null, patterns);

    public static StoreRouteSegments Create(ISession session, params string[] patterns)
    {
        var endpoints = patterns
            .Select((pattern, order) => (Endpoint)new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse(pattern),
                order,
                EndpointMetadataCollection.Empty,
                pattern))
            .ToArray();

        return new StoreRouteSegments(
            new DefaultEndpointDataSource(endpoints),
            session is null ? [] : [Mock.Of<IAutorouteEntries>()],
            session ?? Mock.Of<ISession>());
    }
}
