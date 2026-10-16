using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// Verifies that an Amazon SNS message was signed by Amazon SNS: the signing certificate must come from an Amazon SNS
/// host over HTTPS, and the message's canonical string must verify against it (SHA1 for signature version 1, SHA256 for
/// version 2).
/// </summary>
public sealed class AmazonSnsMessageVerifier
{
    /// <summary>
    /// The name of the HTTP client the signing certificates and subscription confirmations are fetched with.
    /// </summary>
    public const string HttpClientName = "CrestApps.Omnichannel.Email.AmazonSns";

    private const int MaxCachedCertificates = 32;

    private static readonly TimeSpan _certificateLifetime = TimeSpan.FromHours(12);

    // The signing certificates are Amazon's own and the same for every tenant, so one process-wide cache of the few that
    // exist is safe to share; nothing tenant-specific is kept in it.
    private static readonly ConcurrentDictionary<string, (X509Certificate2 Certificate, DateTime ExpiresUtc)> _certificates = new(StringComparer.Ordinal);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmazonSnsMessageVerifier"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="clock">The clock the cached certificates expire by.</param>
    public AmazonSnsMessageVerifier(
        IHttpClientFactory httpClientFactory,
        IClock clock)
    {
        _httpClientFactory = httpClientFactory;
        _clock = clock;
    }

    /// <summary>
    /// Determines whether a URL is one Amazon SNS serves: HTTPS on an <c>sns.&lt;region&gt;.amazonaws.com</c> host. Only
    /// such a URL is ever fetched, so a forged message cannot make the site call anywhere else.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true"/> when the URL belongs to Amazon SNS.</returns>
    public static bool IsAmazonSnsUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.Host;

        return host.StartsWith("sns.", StringComparison.OrdinalIgnoreCase) &&
            (host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase) ||
             host.EndsWith(".amazonaws.com.cn", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds the canonical string Amazon SNS signs for a message.
    /// </summary>
    /// <param name="message">The SNS message.</param>
    /// <returns>The canonical string, or <see langword="null"/> when the message type is unknown.</returns>
    public static string BuildStringToSign(JsonElement message)
    {
        var type = GetString(message, "Type");

        string[] keys = type switch
        {
            "Notification" => ["Message", "MessageId", "Subject", "Timestamp", "TopicArn", "Type"],
            "SubscriptionConfirmation" or "UnsubscribeConfirmation" => ["Message", "MessageId", "SubscribeURL", "Timestamp", "Token", "TopicArn", "Type"],
            _ => null,
        };

        if (keys is null)
        {
            return null;
        }

        var builder = new StringBuilder();

        foreach (var key in keys)
        {
            var value = GetString(message, key);

            // The subject is signed only when the message has one.
            if (value is null)
            {
                continue;
            }

            builder.Append(key).Append('\n').Append(value).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Verifies an SNS message's signature.
    /// </summary>
    /// <param name="message">The SNS message.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when Amazon SNS signed the message.</returns>
    public async Task<bool> VerifyAsync(JsonElement message, CancellationToken cancellationToken = default)
    {
        var certificateUrl = GetString(message, "SigningCertURL") ?? GetString(message, "SigningCertUrl");
        var signature = GetString(message, "Signature");
        var stringToSign = BuildStringToSign(message);

        if (stringToSign is null || string.IsNullOrEmpty(signature) || !IsAmazonSnsUrl(certificateUrl))
        {
            return false;
        }

        var hash = GetString(message, "SignatureVersion") == "2"
            ? HashAlgorithmName.SHA256
            : HashAlgorithmName.SHA1;

        byte[] signatureBytes;

        try
        {
            signatureBytes = Convert.FromBase64String(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var certificate = await GetCertificateAsync(certificateUrl, cancellationToken);

        using var rsa = certificate?.GetRSAPublicKey();

        return rsa is not null &&
            rsa.VerifyData(Encoding.UTF8.GetBytes(stringToSign), signatureBytes, hash, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// Confirms an SNS subscription by visiting its subscribe URL, which must belong to Amazon SNS.
    /// </summary>
    /// <param name="subscribeUrl">The subscribe URL from the confirmation message.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when Amazon SNS confirmed the subscription.</returns>
    public async Task<bool> ConfirmSubscriptionAsync(string subscribeUrl, CancellationToken cancellationToken = default)
    {
        if (!IsAmazonSnsUrl(subscribeUrl))
        {
            return false;
        }

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(subscribeUrl, cancellationToken);

        return response.IsSuccessStatusCode;
    }

    private async Task<X509Certificate2> GetCertificateAsync(string url, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        if (_certificates.TryGetValue(url, out var cached) && cached.ExpiresUtc > now)
        {
            return cached.Certificate;
        }

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        var pem = await client.GetStringAsync(url, cancellationToken);

        X509Certificate2 certificate;

        try
        {
            certificate = X509Certificate2.CreateFromPem(pem);
        }
        catch (CryptographicException)
        {
            return null;
        }

        // Amazon rotates its certificates rarely, so the cache stays tiny; it is emptied if it ever grows past that.
        if (_certificates.Count >= MaxCachedCertificates)
        {
            _certificates.Clear();
        }

        _certificates[url] = (certificate, now.Add(_certificateLifetime));

        return certificate;
    }

    private static string GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
