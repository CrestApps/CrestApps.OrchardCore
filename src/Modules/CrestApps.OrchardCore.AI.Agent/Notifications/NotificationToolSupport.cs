using System.Security.Claims;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.AI.Agent.Notifications;

/// <summary>
/// What the notification tools share. Every tool acts on the notifications of the signed-in caller only: a
/// notification is found by its ID and then checked against the caller, as the Notifications admin page does, so the
/// ID of somebody else's notification finds nothing.
/// </summary>
internal static class NotificationToolSupport
{
    /// <summary>
    /// The largest number of notifications one call of a tool that takes a list acts on.
    /// </summary>
    public const int MaxNotifications = 50;

    /// <summary>
    /// Gets the user ID of the signed-in caller.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The user ID, or <see langword="null"/> when nobody is signed in.</returns>
    public static string GetCallerId(IServiceProvider services)
    {
        var callerId = AgentToolAuthorization.GetCaller(services)?.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(callerId)
            ? null
            : callerId;
    }

    /// <summary>
    /// Finds a notification of the caller by its ID.
    /// </summary>
    /// <param name="manager">The notification manager.</param>
    /// <param name="notificationId">The ID of the notification.</param>
    /// <param name="callerId">The user ID of the caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The notification, or <see langword="null"/> when none has the ID or it was sent to another user.</returns>
    public static async Task<Notification> FindOwnAsync(
        INotificationManager manager,
        string notificationId,
        string callerId,
        CancellationToken cancellationToken)
    {
        var notification = await manager.FindByIdAsync(notificationId, cancellationToken);

        return notification is not null && string.Equals(notification.UserId, callerId, StringComparison.Ordinal)
            ? notification
            : null;
    }

    /// <summary>
    /// Reads the IDs a tool that acts on several notifications was given.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="ids">The IDs without blanks and repeats, or <see langword="null"/> when none is given.</param>
    /// <returns>The sentence the tool returns when the IDs are not a list or too many, or <see langword="null"/>.</returns>
    public static string ReadNotificationIds(AIFunctionArguments arguments, out List<string> ids)
    {
        if (!AgentToolArguments.TryReadStrings(arguments, "notificationIds", out ids))
        {
            return "notificationIds must be a list with the id of each notification, from listNotifications.";
        }

        if (ids is { Count: 0 })
        {
            ids = null;
        }

        return ids?.Count > MaxNotifications
            ? $"one call acts on at most {MaxNotifications} notifications."
            : null;
    }

    /// <summary>
    /// Builds what a tool returns about one notification, from an explicit list of fields.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="notification">The notification.</param>
    /// <returns>The fields the tool returns.</returns>
    public static async Task<object> DescribeAsync(IServiceProvider services, Notification notification)
    {
        return new
        {
            id = notification.NotificationId,
            subject = notification.Subject,
            summary = notification.Summary,
            type = notification.Type,
            read = services.GetRequiredService<INotificationManager>().IsRead(notification),
            createdAt = await AgentToolResults.ToLocalAsync(services.GetRequiredService<ILocalClock>(), notification.CreatedUtc),
            adminUrl = AgentToolLinks.GetAdminPath(services, OrchardCoreFeatureIds.Notifications, "Admin", "Display", new RouteValueDictionary
            {
                { "notificationId", notification.NotificationId },
            }),
        };
    }
}
