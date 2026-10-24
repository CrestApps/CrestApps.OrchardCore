using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// One way an email address can send: Orchard Core's email service, the address's own SMTP server, or a provider's
/// HTTP API a further feature adds. Each address picks its transport, so mailboxes on different providers can live side
/// by side. A transport is registered with <c>services.AddEmailTransport&lt;TTransport&gt;()</c>.
/// </summary>
public interface IEmailTransport
{
    /// <summary>
    /// Gets the transport's technical name, stored on the addresses that use it. It must never change.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown in the address editor.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether the transport sends the email's own headers (<c>Message-ID</c>,
    /// <c>In-Reply-To</c>, <c>References</c>, <c>Auto-Submitted</c>, <c>List-Unsubscribe</c>). A transport that cannot
    /// still threads its replies by subject in most mail clients.
    /// </summary>
    bool SupportsHeaders { get; }

    /// <summary>
    /// Sends one email.
    /// </summary>
    /// <param name="message">The composed email.</param>
    /// <param name="settings">The sending address's settings.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome, with the sent email's <c>Message-ID</c> as the provider message identifier when the
    /// transport set it. A refusal is returned, never thrown.</returns>
    Task<MessageDispatchResult> SendAsync(EmailTransportMessage message, EmailAddressSettings settings, CancellationToken cancellationToken = default);
}
