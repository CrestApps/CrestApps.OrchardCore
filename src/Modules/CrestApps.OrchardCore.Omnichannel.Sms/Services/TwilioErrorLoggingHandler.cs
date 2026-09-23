using System.Text.Json;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// Logs Twilio's own explanation when it refuses a request.
/// </summary>
/// <remarks>
/// The Twilio provider reports a refused message only as "not sent" and discards the response, so a send that
/// fails for bad credentials, a region the account may not text or an unverified trial destination all read the
/// same in the log. Twilio's body names the reason with a code and a message; this records those, and never the
/// request, its headers or its body, which carry the credentials and the recipient.
/// </remarks>
internal sealed class TwilioErrorLoggingHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TwilioErrorLoggingHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public TwilioErrorLoggingHandler(ILogger<TwilioErrorLoggingHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode || response.Content is null || !_logger.IsEnabled(LogLevel.Warning))
        {
            return response;
        }

        // Buffered so the provider can still read the body after it has been read here.
        await response.Content.LoadIntoBufferAsync(cancellationToken);

        var (code, message) = ReadError(await response.Content.ReadAsStringAsync(cancellationToken));

        _logger.LogWarning(
            "Twilio refused the request with HTTP {StatusCode}: error {TwilioErrorCode}, {TwilioErrorMessage}.",
            (int)response.StatusCode,
            code?.SanitizeLogValue() ?? "(none)",
            message?.SanitizeLogValue() ?? "(no message)");

        return response;
    }

    /// <summary>
    /// Reads Twilio's error code and message from a response body.
    /// </summary>
    /// <param name="body">The response body.</param>
    internal static (string Code, string Message) ReadError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            var code = root.TryGetProperty("code", out var codeElement) && codeElement.ValueKind is JsonValueKind.Number or JsonValueKind.String
                ? codeElement.ToString()
                : null;

            var message = root.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()
                : null;

            return (code, message);
        }
        catch (JsonException)
        {
            // Not Twilio's JSON error; there is nothing in it worth recording.
            return (null, null);
        }
    }
}
