using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Provides the default implementation of <see cref="ITelnyxTelephonyCredentialIssuer"/> backed by the
/// Telnyx telephony credentials REST API.
/// </summary>
public sealed class TelnyxTelephonyCredentialIssuer : ITelnyxTelephonyCredentialIssuer
{
    // The maximum number of concurrent live browser credentials a single authenticated user may hold. When a
    // new credential would exceed this cap, the oldest live credentials are revoked first so the newest
    // session wins, bounding how many Telnyx SIP endpoints one agent can materialize. This is a safety net,
    // not the primary cleanup: a renewing soft phone revokes the exact credential it supersedes (see
    // RevokeCredentialAsync), so in normal use the live count stays near one. The cap is kept generous enough
    // that ordinary reloads and renewals do not evict a credential a live browser tab is still registered with
    // (which would surface as a LOGIN_FAILED on that tab).
    private const int MaxLiveCredentialsPerUser = 8;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITelnyxAgentCredentialStore _credentialStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly ISoftPhoneHealthMetrics _healthMetrics;
    private readonly TelnyxOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxTelephonyCredentialIssuer"/> class.
    /// </summary>
    public TelnyxTelephonyCredentialIssuer(
        IHttpClientFactory httpClientFactory,
        ITelnyxAgentCredentialStore credentialStore,
        IClock clock,
        ILogger<TelnyxTelephonyCredentialIssuer> logger,
        ISoftPhoneHealthMetrics healthMetrics,
        IOptionsMonitor<TelnyxOptions> telnyxOptions)
    {
        _httpClientFactory = httpClientFactory;
        _credentialStore = credentialStore;
        _clock = clock;
        _logger = logger;
        _healthMetrics = healthMetrics;
        _options = telnyxOptions.CurrentValue;
    }

    /// <inheritdoc/>
    public async Task<TelnyxTelephonyCredential> IssueAsync(string userId, string displayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (!_options.IsConfigured || string.IsNullOrWhiteSpace(_options.SipConnectionId))
        {
            _logger.LogError("Cannot issue a Telnyx browser credential because the provider is not configured with an API key and a (SIP) connection id.");

            return null;
        }

        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.CredentialLifetimeMinutes);

        var body = new Dictionary<string, object>
        {
            ["connection_id"] = _options.SipConnectionId,
            ["name"] = string.IsNullOrWhiteSpace(displayName) ? $"softphone-{userId}" : displayName,
            ["expires_at"] = expiresAt.ToString("O"),
        };

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync("telephony_credentials", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected a telephony credential request with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());

                _healthMetrics.RecordCredentialFailure();

                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                _logger.LogError("Telnyx returned a telephony credential response without a data object.");

                _healthMetrics.RecordCredentialFailure();

                return null;
            }

            var credentialId = ReadString(data, "id");
            var sipUsername = ReadString(data, "sip_username");
            var sipPassword = ReadString(data, "sip_password");

            if (string.IsNullOrWhiteSpace(credentialId) ||
                string.IsNullOrWhiteSpace(sipUsername) ||
                string.IsNullOrWhiteSpace(sipPassword))
            {
                _logger.LogError("Telnyx returned an incomplete telephony credential (missing id, sip_username, or sip_password).");

                _healthMetrics.RecordCredentialFailure();

                return null;
            }

            await EnforceUserCredentialCapAsync(userId, now, cancellationToken);

            await _credentialStore.CreateAsync(new TelnyxAgentCredential
            {
                UserId = userId.Trim(),
                CredentialId = credentialId,
                SipUsername = sipUsername,
                IssuedUtc = now,
                ExpiresUtc = expiresAt,
            }, cancellationToken);

            _healthMetrics.RecordCredentialIssued();

            return new TelnyxTelephonyCredential
            {
                CredentialId = credentialId,
                SipUsername = sipUsername,
                SipPassword = sipPassword,
                ExpiresAtUtc = expiresAt,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while issuing a Telnyx browser credential.");

            _healthMetrics.RecordCredentialFailure();

            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> RevokeForUserAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        var credentials = await _credentialStore.ListByUserAsync(userId, cancellationToken);
        var revoked = 0;

        foreach (var credential in credentials.Where(credential => !credential.RevokedUtc.HasValue))
        {
            await DeleteAtTelnyxAsync(credential.CredentialId, cancellationToken);
            await _credentialStore.MarkRevokedAsync(credential, _clock.UtcNow, cancellationToken);
            revoked++;
        }

        return revoked;
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeCredentialAsync(string userId, string credentialId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(credentialId))
        {
            return false;
        }

        // Scope the lookup to the caller's own credentials so a user can only ever revoke a credential they own.
        var credentials = await _credentialStore.ListByUserAsync(userId, cancellationToken);

        var credential = credentials.FirstOrDefault(candidate =>
            !candidate.RevokedUtc.HasValue &&
            string.Equals(candidate.CredentialId, credentialId, StringComparison.Ordinal));

        if (credential is null)
        {
            return false;
        }

        await DeleteAtTelnyxAsync(credential.CredentialId, cancellationToken);
        await _credentialStore.MarkRevokedAsync(credential, _clock.UtcNow, cancellationToken);

        return true;
    }

    private async Task EnforceUserCredentialCapAsync(string userId, DateTime now, CancellationToken cancellationToken)
    {
        var live = await _credentialStore.ListLiveByUserAsync(userId, now, cancellationToken);

        if (live.Count < MaxLiveCredentialsPerUser)
        {
            return;
        }

        var overflow = live.Count - MaxLiveCredentialsPerUser + 1;

        foreach (var stale in live.OrderBy(credential => credential.IssuedUtc).Take(overflow))
        {
            await DeleteAtTelnyxAsync(stale.CredentialId, cancellationToken);
            await _credentialStore.MarkRevokedAsync(stale, now, cancellationToken);
        }
    }

    private async Task DeleteAtTelnyxAsync(string credentialId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            return;
        }

        try
        {
            using var client = CreateClient();
            using var response = await client.DeleteAsync($"telephony_credentials/{Uri.EscapeDataString(credentialId)}", cancellationToken);

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Telnyx rejected a telephony credential deletion request for credential {CredentialId} with status code {StatusCode}.",
                    credentialId.SanitizeLogValue(),
                    response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while deleting a Telnyx telephony credential.");
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
    }

    private static string ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

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
