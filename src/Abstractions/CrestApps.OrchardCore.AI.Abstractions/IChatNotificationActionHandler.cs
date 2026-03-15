using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Handles user-initiated actions on chat notification bubbles.
/// When a user clicks an action button on a notification (e.g., "Cancel Transfer"),
/// the hub resolves a keyed service whose key matches the action name.
/// Register implementations using <c>services.AddKeyedScoped&lt;IChatNotificationActionHandler, YourHandler&gt;("your-action-name")</c>.
/// </summary>
public interface IChatNotificationActionHandler
{
    /// <summary>
    /// Handles the notification action triggered by the user.
    /// </summary>
    /// <param name="context">The context containing session details, notification ID, and service provider.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task HandleAsync(ChatNotificationActionContext context, CancellationToken cancellationToken = default);
}
