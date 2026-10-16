using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// Reads one provider's inbound email webhook call into <see cref="Inbound.InboundEmail"/>s. The webhook route names the
/// parser (<c>api/omnichannel/email/inbound/{provider}</c>), so a provider the channel does not know is added by
/// registering another parser with <c>services.AddInboundEmailWebhookParser&lt;TParser&gt;()</c>.
/// </summary>
/// <remarks>
/// Every call has already passed the tenant's webhook key before a parser sees it. A parser checks the provider's own
/// signature on top of that when the provider signs its calls.
/// </remarks>
public interface IInboundEmailWebhookParser
{
    /// <summary>
    /// Gets the parser's name, which is the <c>{provider}</c> segment of the webhook route, such as <c>sendgrid</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Reads a webhook call.
    /// </summary>
    /// <param name="request">The webhook request.</param>
    /// <param name="settings">The tenant's inbound email settings, with the provider signing keys.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The emails the call carried, or why it was refused.</returns>
    Task<InboundEmailWebhookResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default);
}
