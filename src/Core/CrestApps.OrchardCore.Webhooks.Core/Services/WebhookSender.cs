using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Posts a signed webhook body with the egress-guarded <see cref="WebhookStorage.HttpClientName"/> client.
/// </summary>
public sealed class WebhookSender : IWebhookSender
{
    /// <summary>
    /// How much of a refusal's body is kept.
    /// </summary>
    public const int MaxErrorLength = 500;

    /// <summary>
    /// How long a receiver has to answer.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebhookSigner _signer;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="signer">The signer.</param>
    /// <param name="clock">The clock the signature's timestamp comes from.</param>
    /// <param name="logger">The logger.</param>
    public WebhookSender(IHttpClientFactory httpClientFactory, IWebhookSigner signer, IClock clock, ILogger<WebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _signer = signer;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WebhookSendResult> SendAsync(WebhookSendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Uri.TryCreate(request.Url?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return new WebhookSendResult { Error = "The address is not an http or https URL." };
        }

        var body = string.IsNullOrWhiteSpace(request.Body) ? "{}" : request.Body;

        try
        {
            var timestamp = new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds();
            using var message = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            if (!string.IsNullOrEmpty(request.EventId))
            {
                message.Headers.Add(WebhookHeaders.Id, request.EventId);
            }

            if (!string.IsNullOrEmpty(request.EventName))
            {
                message.Headers.Add(WebhookHeaders.Event, request.EventName);
            }

            message.Headers.Add(WebhookHeaders.Timestamp, timestamp.ToString(CultureInfo.InvariantCulture));
            message.Headers.Add(WebhookHeaders.Signature, _signer.Sign(request.Secret, timestamp, body));
            message.Headers.UserAgent.Add(new ProductInfoHeaderValue("CrestApps-Webhooks", "1.0"));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);

            using var response = await _httpClientFactory.CreateClient(WebhookStorage.HttpClientName)
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                return new WebhookSendResult { Succeeded = true, StatusCode = statusCode };
            }

            var start = await ReadStartAsync(response, cancellationToken);

            return new WebhookSendResult
            {
                StatusCode = statusCode,
                Error = Truncate($"{statusCode} {response.ReasonPhrase}: {start}"),
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException && !cancellationToken.IsCancellationRequested)
        {
            // A host the egress guard refused (on the site's own network) is refused again on every attempt.
            var refused = exception.Message.Contains(EgressNetwork.RefusedMessage, StringComparison.Ordinal) ||
                exception.InnerException?.Message.Contains(EgressNetwork.RefusedMessage, StringComparison.Ordinal) == true;

            _logger.LogWarning(exception, "A webhook to '{Host}' failed.", uri.Host);

            return new WebhookSendResult
            {
                EgressRefused = refused,
                Error = Truncate(exception is TaskCanceledException
                    ? $"The receiver did not answer within {Timeout.TotalSeconds:0} seconds."
                    : exception.Message),
            };
        }
    }

    private static string Truncate(string value)
        => string.IsNullOrEmpty(value) || value.Length <= MaxErrorLength ? value : value[..MaxErrorLength] + "…";

    // Only the start of a refusal is kept, and a receiver slow to send it is not waited on.
    private static async Task<string> ReadStartAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer = new char[MaxErrorLength];
            using var reader = new StreamReader(stream);
            var read = await reader.ReadBlockAsync(buffer.AsMemory(), timeout.Token);

            return new string(buffer, 0, read);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or HttpRequestException)
        {
            return string.Empty;
        }
    }
}
