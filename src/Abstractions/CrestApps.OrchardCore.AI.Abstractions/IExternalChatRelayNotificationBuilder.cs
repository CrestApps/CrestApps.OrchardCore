using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Populates a <see cref="ChatNotification"/> and an <see cref="ExternalChatRelayNotificationResult"/>
/// for a specific external chat relay event type.
/// Implementations are registered as <b>keyed scoped services</b> where the key is the event type string
/// (e.g., <see cref="ExternalChatRelayEventTypes.AgentTyping"/>).
/// </summary>
/// <remarks>
/// <para>
/// The <c>DefaultExternalChatRelayEventHandler</c> creates the <see cref="ChatNotification"/>
/// and <see cref="ExternalChatRelayNotificationResult"/>, then resolves the builder by event type key.
/// The builder populates the notification properties and configures the result (e.g., which notifications
/// to remove). This pattern allows multiple builders to contribute to the same notification if needed.
/// </para>
/// <para>
/// To handle a custom event type, register a keyed builder:
/// <code>
/// services.AddKeyedScoped&lt;IExternalChatRelayNotificationBuilder, MyCustomBuilder&gt;("my-custom-event");
/// </code>
/// </para>
/// </remarks>
public interface IExternalChatRelayNotificationBuilder
{
    /// <summary>
    /// Populates the notification and result for the given relay event.
    /// </summary>
    /// <param name="relayEvent">The event received from the external system.</param>
    /// <param name="notification">The notification object to populate with content, icon, type, etc.</param>
    /// <param name="result">The result to configure with notification IDs to remove.</param>
    /// <param name="T">The string localizer for translating user-facing messages.</param>
    void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T);
}
