using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// Signs ACP order updates with the platform's secret: <c>Merchant-Signature: t=…,v1=…</c> (HMAC-SHA256 of the
/// timestamp and the body).
/// </summary>
public sealed class AcpOrderWebhookFormatter : IWebhookDeliveryFormatter
{
    /// <summary>
    /// The formatter's name.
    /// </summary>
    public const string FormatterName = "acp-order";

    private readonly IAgentPlatformStore _platforms;
    private readonly IDataProtector _protector;

    public AcpOrderWebhookFormatter(IAgentPlatformStore platforms, IDataProtectionProvider dataProtectionProvider)
    {
        _platforms = platforms;
        _protector = dataProtectionProvider.CreateProtector(AgenticCommerceStorage.SecretProtectionPurpose);
    }

    /// <inheritdoc />
    public string Name
        => FormatterName;

    /// <inheritdoc />
    public async Task<WebhookFormattedRequest> FormatAsync(WebhookFormatContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Data.TryGetValue(AcpConstants.PlatformIdData, out var platformId) ||
            await _platforms.FindByIdAsync(platformId, cancellationToken) is not { } platform)
        {
            return WebhookFormattedRequest.Fail("The agent platform was removed.");
        }

        string secret;

        try
        {
            secret = string.IsNullOrEmpty(platform.ProtectedWebhookSecret) ? null : _protector.Unprotect(platform.ProtectedWebhookSecret);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            secret = null;
        }

        if (string.IsNullOrEmpty(secret))
        {
            return WebhookFormattedRequest.Fail("The platform's signing secret can't be read. Give it a new secret, then send the update again.");
        }

        var formatted = new WebhookFormattedRequest();

        formatted.Headers["Merchant-Signature"] = TimestampedHmacSignature.Sign(secret, context.Timestamp, context.Body ?? "{}");
        formatted.Headers["Request-Id"] = context.EventId;
        formatted.Headers["Timestamp"] = DateTimeOffset.FromUnixTimeSeconds(context.Timestamp).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

        return formatted;
    }
}
