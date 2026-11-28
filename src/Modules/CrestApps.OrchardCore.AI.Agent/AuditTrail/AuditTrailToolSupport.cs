using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.AuditTrail.Models;
using OrchardCore.AuditTrail.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.AuditTrail;

/// <summary>
/// What the audit trail tools share: the fields a tool returns for every event, and the address of the admin page of
/// an event.
/// </summary>
internal static class AuditTrailToolSupport
{
    /// <summary>
    /// Builds the fields a tool returns for every event: what happened, who did it and when. The IP address and the
    /// data of the event are not among them.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="auditTrailEvent">The event.</param>
    /// <returns>The fields the tool returns.</returns>
    public static async Task<AuditTrailEventSummary> SummarizeAsync(IServiceProvider services, AuditTrailEvent auditTrailEvent)
    {
        var descriptor = services.GetRequiredService<IAuditTrailManager>().DescribeEvent(auditTrailEvent);

        return new AuditTrailEventSummary
        {
            EventId = auditTrailEvent.EventId,
            Category = auditTrailEvent.Category,
            Event = auditTrailEvent.Name,

            // The name the Audit Trail admin page shows for the event.
            Title = descriptor?.LocalizedName?.Invoke(services)?.Value,
            CorrelationId = string.IsNullOrEmpty(auditTrailEvent.CorrelationId)
                ? null
                : auditTrailEvent.CorrelationId,
            UserId = string.IsNullOrEmpty(auditTrailEvent.UserId)
                ? null
                : auditTrailEvent.UserId,
            UserName = auditTrailEvent.UserName,
            CreatedAt = await AgentToolResults.ToLocalAsync(services.GetRequiredService<ILocalClock>(), auditTrailEvent.CreatedUtc),
        };
    }

    /// <summary>
    /// Gets the address of the admin page that shows an event.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="eventId">The ID of the event.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetAdminUrl(IServiceProvider services, string eventId)
    {
        return AgentToolLinks.GetAdminPath(services, OrchardCoreFeatureIds.AuditTrail, "Admin", "Display", new RouteValueDictionary
        {
            { "auditTrailEventId", eventId },
        });
    }
}
