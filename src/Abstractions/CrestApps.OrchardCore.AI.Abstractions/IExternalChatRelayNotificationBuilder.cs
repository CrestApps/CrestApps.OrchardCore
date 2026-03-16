using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Builds a <see cref="ExternalChatRelayNotificationResult"/> for a specific external chat relay event type.
/// Implementations are registered as <b>keyed scoped services</b> where the key is the event type string
/// (e.g., <see cref="ExternalChatRelayEventTypes.AgentTyping"/>).
/// </summary>
/// <remarks>
/// <para>
/// The <c>DefaultExternalChatRelayEventHandler</c> resolves the builder by event type key. If a builder
/// is found, it calls <see cref="Build"/> to produce a <see cref="ExternalChatRelayNotificationResult"/>,
/// then delegates to <see cref="IExternalChatRelayNotificationHandler"/> to send/remove notifications.
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
    /// Builds the notification result for the given relay event.
    /// </summary>
    /// <param name="relayEvent">The event received from the external system.</param>
    /// <param name="localizer">The string localizer for translating user-facing messages.</param>
    /// <returns>
    /// A <see cref="ExternalChatRelayNotificationResult"/> describing which notifications
    /// to remove and/or which new notification to send.
    /// </returns>
    ExternalChatRelayNotificationResult Build(ExternalChatRelayEvent relayEvent, IStringLocalizer localizer);
}
