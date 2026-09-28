using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// A lightweight search endpoint that lists Contact Center queues for the reusable item selector (used, for
/// example, by the SMS Portal routing editor to pick a department queue). It returns the enabled queues,
/// optionally filtered by name, as {value, text} pairs.
/// </summary>
internal static class QueueSearchEndpoints
{
    public const string RouteName = "CrestApps.ContactCenter.QueueSearch";

    public static IEndpointRouteBuilder AddQueueSearchEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/api/crestapps/contact-center/queues/search", HandleAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string query,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IActivityQueueManager queueManager,
        CancellationToken cancellationToken)
    {
        // Signing in used to be enough, so any registered user could list every queue by name. Only the people who
        // pick a queue -- those who manage queues, or the channel endpoints whose routing names one -- see the list.
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.ManageQueues) &&
            !await authorizationService.AuthorizeAsync(httpContext.User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Results.Forbid();
        }

        var queues = await queueManager.GetEnabledAsync(cancellationToken);

        IEnumerable<Core.Models.ActivityQueue> matches = queues;

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
