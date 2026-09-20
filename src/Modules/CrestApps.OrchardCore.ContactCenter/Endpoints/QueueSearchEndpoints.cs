using CrestApps.Core.ContactCenter.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// A lightweight search endpoint that lists Contact Center queues for the reusable item selector (used, for
/// example, by the SMS Portal routing editor to pick a department queue). It returns the enabled queues,
/// optionally filtered by name, as {value, text} pairs.
/// </summary>
public static class QueueSearchEndpoints
{
    public const string RouteName = "CrestApps.ContactCenter.QueueSearch";

    /// <summary>
    /// Maps the queue search endpoint.
    /// </summary>
    /// <remarks>
    /// Queue lookup for the pickers that assign work.
    /// </remarks>
    /// <param name="builder">The route builder to map onto.</param>
    /// <param name="configure">
    /// Applied to every route mapped here, so a host can add its own filters or metadata.
    /// </param>
    /// <returns>The same route builder, so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapContactCenterQueueSearchEndpoints(
        this IEndpointRouteBuilder builder,
        Action<RouteHandlerBuilder> configure = null)
    {
        var route = builder.MapGet("Admin/api/crestapps/contact-center/queues/search", HandleAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        configure?.Invoke(route);

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        string query,
        IActivityQueueManager queueManager,
        CancellationToken cancellationToken)
    {
        var queues = await queueManager.GetEnabledAsync(cancellationToken);

        IEnumerable<CrestApps.Core.ContactCenter.Models.ActivityQueue> matches = queues;

        if (!string.IsNullOrWhiteSpace(query))
        {
            matches = queues.Where(queue =>
                !string.IsNullOrEmpty(queue.Name) &&
                queue.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var items = matches
            .OrderBy(queue => queue.Name, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .Select(queue => new { value = queue.ItemId, text = queue.Name });

        return Results.Ok(items);
    }
}
