using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Endpoints;

/// <summary>
/// The site's webhook key check, shared by the inbound email and the delivery events webhooks.
/// </summary>
internal static class EmailWebhookKey
{
    /// <summary>
    /// Checks the key a webhook call carries, in the <c>key</c> query value or the webhook key header.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="settings">The site's inbound email settings.</param>
    /// <param name="secretProtector">The protector the stored key is read with.</param>
    /// <returns>
    /// <see langword="null"/> when the tenant has no key (the webhook is not set up and should answer as if it did not
    /// exist), otherwise whether the call carried the right key.
    /// </returns>
    public static bool? Check(HttpRequest request, EmailInboundSettings settings, IEmailSecretProtector secretProtector)
    {
        var key = secretProtector.Unprotect(settings?.WebhookKey);

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var provided = request.Query["key"].ToString();

        if (string.IsNullOrEmpty(provided))
        {
            provided = request.Headers[EmailChannelConstants.WebhookKeyHeaderName].ToString();
        }

        return !string.IsNullOrEmpty(provided) &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(provided));
    }
}
