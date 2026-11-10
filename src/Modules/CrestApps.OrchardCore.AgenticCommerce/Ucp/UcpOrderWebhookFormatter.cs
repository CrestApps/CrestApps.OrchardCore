using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Signs UCP order updates as UCP requires: <c>UCP-Agent</c> naming the store's profile, <c>Webhook-Id</c> and
/// <c>Webhook-Timestamp</c>, a <c>Content-Digest</c>, and an RFC 9421 signature with the store's current key.
/// </summary>
public sealed class UcpOrderWebhookFormatter : IWebhookDeliveryFormatter
{
    /// <summary>
    /// The formatter's name.
    /// </summary>
    public const string FormatterName = "ucp-order";

    private static readonly string[] _covered = ["@method", "@authority", "@path", "content-digest", "content-type"];

    private readonly IMerchantSigningKeyService _keys;
    private readonly AgentSiteUrls _urls;

    public UcpOrderWebhookFormatter(IMerchantSigningKeyService keys, AgentSiteUrls urls)
    {
        _keys = keys;
        _urls = urls;
    }

    /// <inheritdoc />
    public string Name
        => FormatterName;

    /// <inheritdoc />
    public async Task<WebhookFormattedRequest> FormatAsync(WebhookFormatContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!Uri.TryCreate(context.Url, UriKind.Absolute, out var uri))
        {
            return WebhookFormattedRequest.Fail("The platform's order updates address isn't valid.");
        }

        var body = Encoding.UTF8.GetBytes(context.Body ?? "{}");
        var digest = ContentDigest.ComputeSha256(body);
        var key = await _keys.GetSigningKeyAsync(cancellationToken);
        var message = new HttpSignatureMessage
        {
            Method = "POST",
            Scheme = uri.Scheme,
            Authority = uri.IsDefaultPort ? uri.Host.ToLowerInvariant() : $"{uri.Host.ToLowerInvariant()}:{uri.Port}",
            Path = uri.AbsolutePath,
            Query = string.IsNullOrEmpty(uri.Query) ? null : uri.Query,
        };

        message.Headers["content-digest"] = digest;
        message.Headers["content-type"] = "application/json; charset=utf-8";

        using var ecdsa = key.CreateECDsa();
        var signature = HttpMessageSignatures.Sign(message, _covered, key.Kid, ecdsa);
        var formatted = new WebhookFormattedRequest();

        formatted.Headers[UcpConstants.AgentHeader] = $"profile=\"{await _urls.GetBaseUrlAsync()}/{UcpConstants.ProfilePath}\"";
        formatted.Headers["Webhook-Id"] = context.EventId;
        formatted.Headers["Webhook-Timestamp"] = context.Timestamp.ToString(CultureInfo.InvariantCulture);
        formatted.Headers["Content-Digest"] = digest;
        formatted.Headers["Content-Type"] = "application/json; charset=utf-8";
        formatted.Headers["Signature-Input"] = signature.SignatureInput;
        formatted.Headers["Signature"] = signature.Signature;

        return formatted;
    }
}
