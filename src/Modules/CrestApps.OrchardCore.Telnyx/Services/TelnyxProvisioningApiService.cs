using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default implementation of <see cref="ITelnyxProvisioningApiService"/>. It calls the Telnyx REST API with
/// the account API key to find-or-create the Call Control application, Credential connection, and outbound
/// voice profile, and to discover numbers for the caller id.
/// </summary>
public sealed class TelnyxProvisioningApiService : ITelnyxProvisioningApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxProvisioningApiService"/> class.
    /// </summary>
    public TelnyxProvisioningApiService(
        IHttpClientFactory httpClientFactory,
        ILogger<TelnyxProvisioningApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TelnyxProvisioningResult> ConnectAsync(
        string apiKey,
        string apiBaseUrl,
        string webhookUrl,
        string resourceName,
        CancellationToken cancellationToken = default)
    {
        var result = new TelnyxProvisioningResult();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            result.Error = "A Telnyx API key is required to connect.";

            return result;
        }

        using var client = CreateClient(apiKey, apiBaseUrl);

        // Telnyx enforces unique connection names across ALL connection types, so a Call Control
        // application and a Credential connection cannot share a name. Each resource therefore gets a
        // distinct, stable name so find-or-create stays idempotent without colliding with the others.
        var callControlName = resourceName;
        var credentialName = $"{resourceName} SIP";
        var outboundProfileName = resourceName;

        try
        {
            // Critical: the Call Control application id is the call connection_id, and the Credential
            // connection id is what browser telephony credentials are minted against. Both must succeed.
            var callControl = await FindOrCreateCallControlApplicationAsync(client, callControlName, webhookUrl, cancellationToken);
            result.ConnectionId = callControl.Id;

            if (string.IsNullOrWhiteSpace(result.ConnectionId))
            {
                result.Error = DescribeCreateFailure("Call Control application", callControl.Error);

                return result;
            }

            var credentialConnection = await FindOrCreateCredentialConnectionAsync(client, credentialName, cancellationToken);
            result.SipConnectionId = credentialConnection.Id;

            if (string.IsNullOrWhiteSpace(result.SipConnectionId))
            {
                result.Error = DescribeCreateFailure("Credential SIP connection", credentialConnection.Error);

                return result;
            }

            result.Succeeded = true;

            // Best-effort: an outbound voice profile bound to both connections lets calls leave the account.
            try
            {
                result.OutboundVoiceProfileId = await FindOrCreateOutboundVoiceProfileAsync(client, outboundProfileName, cancellationToken);

                if (!string.IsNullOrWhiteSpace(result.OutboundVoiceProfileId))
                {
                    await BindOutboundProfileAsync(client, "call_control_applications", result.ConnectionId, result.OutboundVoiceProfileId, cancellationToken);
                    await BindOutboundProfileAsync(client, "credential_connections", result.SipConnectionId, result.OutboundVoiceProfileId, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Telnyx connect completed the connections but could not provision or bind an outbound voice profile.");
                result.Warning = "Connected, but an outbound voice profile could not be created automatically. Create one in the Telnyx portal and set it in the advanced settings if outbound calls fail.";
            }

            // Best-effort: discover numbers so the admin can pick a caller id, and assign an unassigned number
            // to the Call Control application so inbound calls reach the webhook.
            try
            {
                await DiscoverNumbersAsync(client, result, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Telnyx connect could not enumerate the account's numbers.");
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while connecting the app to Telnyx.");
            result.Succeeded = false;
            result.Error = "An error occurred while connecting to Telnyx. Check the logs and verify the API key.";

            return result;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DisconnectAsync(
        string apiKey,
        string apiBaseUrl,
        string connectionId,
        string sipConnectionId,
        string outboundVoiceProfileId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        using var client = CreateClient(apiKey, apiBaseUrl);
        var ok = true;

        // Delete connections before the profile they reference.
        ok &= await DeleteResourceAsync(client, "call_control_applications", connectionId, cancellationToken);
        ok &= await DeleteResourceAsync(client, "credential_connections", sipConnectionId, cancellationToken);
        ok &= await DeleteResourceAsync(client, "outbound_voice_profiles", outboundVoiceProfileId, cancellationToken);

        return ok;
    }

    private async Task<(string Id, string Error)> FindOrCreateCallControlApplicationAsync(HttpClient client, string name, string webhookUrl, CancellationToken cancellationToken)
    {
        var existing = await FindByPropertyAsync(client, "call_control_applications", "application_name", name, cancellationToken);

        if (!string.IsNullOrWhiteSpace(existing))
        {
            // Keep the webhook URL current on re-connect.
            await PatchAsync(client, "call_control_applications", existing, new Dictionary<string, object>
            {
                ["webhook_event_url"] = webhookUrl,
                ["webhook_api_version"] = "2",
            }, cancellationToken);

            return (existing, null);
        }

        return await CreateAsync(client, "call_control_applications", new Dictionary<string, object>
        {
            ["application_name"] = name,
            ["webhook_event_url"] = webhookUrl,
            ["webhook_api_version"] = "2",
        }, cancellationToken);
    }

    private async Task<(string Id, string Error)> FindOrCreateCredentialConnectionAsync(HttpClient client, string name, CancellationToken cancellationToken)
    {
        var existing = await FindByPropertyAsync(client, "credential_connections", "connection_name", name, cancellationToken);

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return (existing, null);
        }

        // A Credential connection requires a static user name/password. The browser soft phone does not use
        // these — it mints short-lived telephony credentials against this connection — but Telnyx requires
        // them to create the connection, so a random pair is generated.
        return await CreateAsync(client, "credential_connections", new Dictionary<string, object>
        {
            ["connection_name"] = name,
            ["user_name"] = "cc" + RandomToken(14),
            ["password"] = RandomToken(24),
        }, cancellationToken);
    }

    private async Task<string> FindOrCreateOutboundVoiceProfileAsync(HttpClient client, string name, CancellationToken cancellationToken)
    {
        var existing = await FindByPropertyAsync(client, "outbound_voice_profiles", "name", name, cancellationToken);

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var (id, _) = await CreateAsync(client, "outbound_voice_profiles", new Dictionary<string, object>
        {
            ["name"] = name,
            ["traffic_type"] = "conversational",
            ["service_plan"] = "global",
        }, cancellationToken);

        return id;
    }

    private async Task BindOutboundProfileAsync(HttpClient client, string resource, string id, string outboundVoiceProfileId, CancellationToken cancellationToken)
    {
        await PatchAsync(client, resource, id, new Dictionary<string, object>
        {
            ["outbound"] = new Dictionary<string, object>
            {
                ["outbound_voice_profile_id"] = outboundVoiceProfileId,
            },
        }, cancellationToken);
    }

    private async Task DiscoverNumbersAsync(HttpClient client, TelnyxProvisioningResult result, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("phone_numbers?page[size]=250", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        string firstUnassignedId = null;

        foreach (var number in data.EnumerateArray())
        {
            var phoneNumber = ReadString(number, "phone_number");

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                continue;
            }

            result.AvailableNumbers.Add(phoneNumber);

            var connectionId = ReadString(number, "connection_id");

            if (firstUnassignedId is null && string.IsNullOrWhiteSpace(connectionId))
            {
                firstUnassignedId = ReadString(number, "id");
            }
        }

        result.SuggestedCallerId = result.AvailableNumbers.FirstOrDefault();

        // Assign one unassigned number to the Call Control application so inbound calls to it reach the
        // webhook. Never reassign a number already bound to another connection.
        if (!string.IsNullOrWhiteSpace(firstUnassignedId) && !string.IsNullOrWhiteSpace(result.ConnectionId))
        {
            await PatchAsync(client, "phone_numbers", firstUnassignedId, new Dictionary<string, object>
            {
                ["connection_id"] = result.ConnectionId,
            }, cancellationToken);
        }
    }

    private async Task<string> FindByPropertyAsync(HttpClient client, string resource, string property, string value, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"{resource}?page[size]=250", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Telnyx rejected a {Resource} list request with status code {StatusCode} during connect.",
                resource,
                response.StatusCode);

            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in data.EnumerateArray())
        {
            if (string.Equals(ReadString(item, property), value, StringComparison.Ordinal))
            {
                return ReadString(item, "id");
            }
        }

        return null;
    }

    private async Task<(string Id, string Error)> CreateAsync(HttpClient client, string resource, IDictionary<string, object> body, CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
        using var response = await client.PostAsync(resource, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var payload = await SafeReadContentAsync(response, cancellationToken);

            _logger.LogError(
                "Telnyx rejected a {Resource} creation request with status code {StatusCode}. Response: {Response}",
                resource,
                response.StatusCode,
                payload.SanitizeLogValue());

            return (null, ParseTelnyxError(payload, response.StatusCode));
        }

        return (await ReadDataStringAsync(response, "id", cancellationToken), null);
    }

    private static string DescribeCreateFailure(string resourceLabel, string telnyxError)
    {
        var reason = string.IsNullOrWhiteSpace(telnyxError)
            ? "Telnyx rejected the request."
            : telnyxError;

        var message = $"Telnyx could not create the {resourceLabel}. {reason}";

        // Only add the permissions guidance when the error actually looks like an authorization failure, so
        // unrelated errors (for example a name collision) are not muddied by advice that does not apply.
        if (LooksLikePermissionError(telnyxError))
        {
            message += " Create the API key while signed in as an account Owner/Admin so it can create " +
                "resources (a restricted member cannot), and make sure Programmable Voice is enabled, then try Connect again.";
        }

        return message;
    }

    private static bool LooksLikePermissionError(string telnyxError)
    {
        if (string.IsNullOrWhiteSpace(telnyxError))
        {
            return false;
        }

        return telnyxError.Contains("10006", StringComparison.Ordinal) ||
            telnyxError.Contains("10009", StringComparison.Ordinal) ||
            telnyxError.Contains("not authorized", StringComparison.OrdinalIgnoreCase) ||
            telnyxError.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
            telnyxError.Contains("forbidden", StringComparison.OrdinalIgnoreCase);
    }

    private static string ParseTelnyxError(string payload, HttpStatusCode statusCode)
    {
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var document = JsonDocument.Parse(payload);

                if (document.RootElement.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Array &&
                    errors.GetArrayLength() > 0)
                {
                    var first = errors[0];
                    var title = ReadString(first, "title");
                    var detail = ReadString(first, "detail");
                    var code = ReadString(first, "code");
                    var message = string.IsNullOrWhiteSpace(detail) ? title : detail;

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return string.IsNullOrWhiteSpace(code) ? message : $"{message} (Telnyx code {code})";
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to the status-code description.
            }
        }

        return $"Telnyx returned {(int)statusCode} {statusCode}.";
    }

    private async Task PatchAsync(HttpClient client, string resource, string id, IDictionary<string, object> body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{resource}/{Uri.EscapeDataString(id)}")
        {
            Content = content,
        };
        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Telnyx rejected a {Resource} update request with status code {StatusCode} during connect.",
                resource,
                response.StatusCode);
        }
    }

    private async Task<bool> DeleteResourceAsync(HttpClient client, string resource, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return true;
        }

        using var response = await client.DeleteAsync($"{resource}/{Uri.EscapeDataString(id)}", cancellationToken);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return true;
        }

        _logger.LogWarning(
            "Telnyx rejected a {Resource} deletion request for {Id} with status code {StatusCode} during disconnect.",
            resource,
            id.SanitizeLogValue(),
            response.StatusCode);

        return false;
    }

    private HttpClient CreateClient(string apiKey, string apiBaseUrl)
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        var resolved = string.IsNullOrWhiteSpace(apiBaseUrl)
            ? TelnyxConstants.DefaultApiBaseUrl
            : apiBaseUrl.EndsWith('/') ? apiBaseUrl : apiBaseUrl + '/';

        client.BaseAddress = new Uri(resolved);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return client;
    }

    private static string ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static async Task<string> ReadDataStringAsync(HttpResponseMessage response, string propertyName, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore malformed responses.
        }

        return null;
    }

    private static string RandomToken(int length)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Span<char> buffer = stackalloc char[length];

        for (var i = 0; i < length; i++)
        {
            buffer[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(buffer);
    }

    private static async Task<string> SafeReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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
            return string.Empty;
        }
    }
}
