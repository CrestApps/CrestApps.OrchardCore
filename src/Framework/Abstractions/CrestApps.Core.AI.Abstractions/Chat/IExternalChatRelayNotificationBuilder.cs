using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Populates a <see cref="ChatNotification"/> and an <see cref="ExternalChatRelayNotificationResult"/>
/// for a specific external chat relay event type.
/// Implementations are registered as <b>keyed scoped services</b> where the key is the event type string
/// (e.g., <see cref="ExternalChatRelayEventTypes.AgentTyping"/>).
/// </summary>
/// <remarks>
/// <para>
/// The <c>DefaultExternalChatRelayEventHandler</c> resolves the builder by event type key,
/// creates the <see cref="ChatNotification"/> with <see cref="ChatNotification.Type"/> set from
/// <see cref="NotificationType"/>, and then calls <see cref="Build"/> to populate remaining properties.
/// Builders should not modify <see cref="ChatNotification.Type"/> — this allows the handler to control
/// the type and enables multiple builders to contribute without overriding each other.
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
    /// Gets the notification type that this builder produces. The type serves as both the
    /// unique identifier and the CSS styling class. Use constants from
    /// <see cref="ChatNotificationTypes"/> for built-in types, or any custom string.
    /// This value is used by the handler to create the <see cref="ChatNotification"/> with its
    /// <see cref="ChatNotification.Type"/> pre-set. Builders should not modify the type in
    /// <see cref="Build"/>.
    /// Return <see langword="null"/> for removal-only builders that do not send a notification.
    /// </summary>
    string NotificationType { get; }

    /// <summary>
    /// Populates the notification and result for the given relay event.
    /// The <see cref="ChatNotification.Type"/> is already set by the handler via
    /// <see cref="NotificationType"/> — do not override it in this method.
    /// </summary>
    /// <param name="relayEvent">The event received from the external system.</param>
    /// <param name="notification">The notification object to populate with content, icon, etc.</param>
    /// <param name="result">The result to configure with notification types to remove.</param>
    /// <param name="T">The string localizer for translating user-facing messages.</param>
    void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T);
}
