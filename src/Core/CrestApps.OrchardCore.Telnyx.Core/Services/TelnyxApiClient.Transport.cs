using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Sends a command to the Telnyx API and reads its answer: the bearer per request and the retries the policy allows,
/// each retry counted on <c>crestapps.retry.attempts</c>.
/// </summary>
public sealed partial class TelnyxApiClient
{
    // The value of the area tag on the shared retry counter for this client's retries.
    private const string TelnyxRetryArea = "telnyx";

    private async Task<(TelnyxApiResult Result, JsonElement? Json)> SendAsync(
        HttpMethod method,
        string path,
        IDictionary<string, object> body,
        bool retryable,
        CancellationToken cancellationToken)
    {
        var attempts = retryable ? TelnyxApiRetryPolicy.MaxAttempts : 1;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, path);

                // The bearer is attached per request rather than on the shared client, because the client is
                // registered once for the process while the key is a per-tenant setting.
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentOptions.ApiKey);

                if (body is not null)
                {
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(body, _serializerOptions),
                        Encoding.UTF8,
                        "application/json");
                }

                // The command and its body. Until this was logged, the only trace of an answer, dial or bridge
                // was the transport layer's "Sending HTTP request POST .../calls" line -- which leg was bridged
                // to which, what a dial asked for, and what a command omitted (a codec preference, say) could
                // be established only by reading the code, never from the log of the call in question.
                if (body is not null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Telnyx command {Method} {Path} body: {Body}",
                        method,
                        path.SanitizeLogValue(),
                        JsonSerializer.Serialize(body, _serializerOptions));
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var content = await ReadContentAsync(response, cancellationToken);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Telnyx command {Method} {Path} returned {StatusCode}.",
                        method,
                        path.SanitizeLogValue(),
                        (int)response.StatusCode);
                }

                if (response.IsSuccessStatusCode)
                {
                    return (TelnyxApiResult.Success(response.StatusCode), TryParse(content));
                }

                if (attempt < attempts && TelnyxApiRetryPolicy.IsRetryable(response.StatusCode))
                {
                    var delay = _retryPolicy.GetDelay(attempt, response.Headers.RetryAfter?.Delta);

                    RetryTelemetry.RecordRetry(TelnyxRetryArea, GetRetryReason(response.StatusCode), attempt + 1);

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Telnyx answered {StatusCode} for {Method} {Path}; retrying attempt {Attempt} of {Attempts} after {Delay}.",
                            (int)response.StatusCode,
                            method,
                            path.SanitizeLogValue(),
                            attempt + 1,
                            attempts,
                            delay);
                    }

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }

                    continue;
                }

                var failure = TelnyxApiResult.Failure(response.StatusCode, content);

                // A hangup refused because the call has already ended got what it asked for: the caller put the phone
                // down first. It was logged as a warning at the end of nearly every call the customer finished.
                if (path.EndsWith("/actions/hangup", StringComparison.OrdinalIgnoreCase) && TelnyxApiErrors.IsCallAlreadyEnded(failure))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Telnyx had already ended the call {Path} was asked to hang up.", path.SanitizeLogValue());
                    }

                    return (failure, null);
                }

                // Why Telnyx refused the command. Only the status was logged, so a call whose answer, menu and hold music
                // were all refused with 422 left no trace of the reason (a call already answered elsewhere, a leg on
                // another connection, a call that had ended).
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "Telnyx refused {Method} {Path} with {StatusCode}: {Errors}",
                        method,
                        path.SanitizeLogValue(),
                        (int)response.StatusCode,
                        DescribeErrors(content));
                }

                return (failure, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt < attempts)
                {
                    RetryTelemetry.RecordRetry(TelnyxRetryArea, GetRetryReason(ex), attempt + 1);

                    continue;
                }

                // A provider that cannot be reached is a failed command, not an unhandled exception on a path
                // that is holding a live call.
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "The Telnyx request {Method} {Path} could not be completed.", method, path.SanitizeLogValue());
                }

                return (TelnyxApiResult.Failure(null, ex.Message), null);
            }
        }
    }

    private static string GetRetryReason(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.TooManyRequests => "status_429",
            HttpStatusCode.RequestTimeout => "status_408",
            _ => "status_5xx",
        };

    private static string GetRetryReason(Exception exception)
        => exception is TaskCanceledException or TimeoutException
            ? CrestAppsOrchardCoreTelemetry.OutcomeTimeout
            : "network";

    private static async Task<string> ReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // A body that cannot be read does not change whether the command succeeded.
            return string.Empty;
        }
    }

    // Telnyx explains a refusal as {"errors":[{"code":"90018","title":"...","detail":"..."}]}. Only those fields are kept,
    // so nothing else the response might carry reaches the log.
    private static string DescribeErrors(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "(no body)";
        }

        try
        {
            using var document = JsonDocument.Parse(content);

            if (!document.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
            {
                return "(no errors listed)";
            }

            static string Read(JsonElement error, string name)
                => error.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            return string.Join("; ", errors.EnumerateArray()
                .Where(error => error.ValueKind == JsonValueKind.Object)
                .Select(error => $"{Read(error, "code")} {Read(error, "title")}: {Read(error, "detail")}".Trim()));
        }
        catch (JsonException)
        {
            return "(unreadable body)";
        }
    }

    private static JsonElement? TryParse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);

            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
