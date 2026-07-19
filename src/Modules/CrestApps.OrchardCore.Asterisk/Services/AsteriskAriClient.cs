using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;
using Polly.Timeout;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Provides tenant-scoped Asterisk ARI operations over the configured named HTTP client.
/// </summary>
internal sealed class AsteriskAriClient : IAsteriskAriClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DefaultAsteriskOptions _defaultOptions;
    private readonly ShellSettings _shellSettings;
    private readonly IAsteriskAriApplicationGate _applicationGate;
    private readonly ILogger<AsteriskAriClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskAriClient"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read tenant Asterisk settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect tenant secrets.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="defaultOptions">The configuration-backed default Asterisk options.</param>
    /// <param name="shellSettings">The current tenant shell settings used to scope the host-default fallback.</param>
    /// <param name="applicationGate">The gate that enforces single-tenant ownership of each ARI application.</param>
    /// <param name="logger">The logger instance.</param>
    public AsteriskAriClient(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IOptions<DefaultAsteriskOptions> defaultOptions,
        ShellSettings shellSettings,
        IAsteriskAriApplicationGate applicationGate,
        ILogger<AsteriskAriClient> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _httpClientFactory = httpClientFactory;
        _defaultOptions = defaultOptions.Value;
        _shellSettings = shellSettings;
        _applicationGate = applicationGate;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AsteriskAriChannel> OriginateAsync(
        AsteriskAriOriginateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = ResolveSettings(nameof(OriginateAsync));
        ValidateOriginateRequest(request);

        var app = string.IsNullOrWhiteSpace(request.App)
            ? settings.ApplicationName
            : request.App;
        var query = new Dictionary<string, string>
        {
            ["endpoint"] = request.Endpoint,
            ["app"] = app,
            ["channelId"] = request.ChannelId,
            ["appArgs"] = string.Join(',', request.AppArgs ?? Array.Empty<string>()),
        };

        if (!string.IsNullOrWhiteSpace(request.CallerId))
        {
            query["callerId"] = request.CallerId;
        }

        var timeoutSeconds = request.TimeoutSeconds > 0
            ? request.TimeoutSeconds
            : settings.TimeoutSeconds;

        if (timeoutSeconds > 0)
        {
            query["timeout"] = timeoutSeconds.ToString(CultureInfo.InvariantCulture);
        }

        var variables = request.Variables is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(request.Variables, StringComparer.Ordinal);
        variables[AsteriskConstants.OriginationMarkerVariableName] = AsteriskAriConstants.OriginationMarkerValue;

        using var content = JsonContent.Create(new AsteriskAriVariablesPayload
        {
            Variables = variables,
        }, options: JsonOptions);
        using var response = await SendAsync(settings, HttpMethod.Post, "channels", query, content, nameof(OriginateAsync), cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return await GetChannelAsync(settings, request.ChannelId, nameof(OriginateAsync), cancellationToken);
        }

        await EnsureSuccessAsync(response, nameof(OriginateAsync), cancellationToken);

        return await ReadJsonAsync<AsteriskAriChannel>(response, nameof(OriginateAsync), cancellationToken) ??
            await GetChannelAsync(settings, request.ChannelId, nameof(OriginateAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AsteriskAriBridge> CreateBridgeAsync(
        string bridgeId,
        string bridgeType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(bridgeId);
        ArgumentException.ThrowIfNullOrEmpty(bridgeType);

        var settings = ResolveSettings(nameof(CreateBridgeAsync));
        var query = new Dictionary<string, string>
        {
            ["type"] = bridgeType,
        };
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"bridges/{Uri.EscapeDataString(bridgeId)}",
            query,
            null,
            nameof(CreateBridgeAsync),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return await GetBridgeAsync(settings, bridgeId, nameof(CreateBridgeAsync), cancellationToken);
        }

        await EnsureSuccessAsync(response, nameof(CreateBridgeAsync), cancellationToken);

        return await ReadJsonAsync<AsteriskAriBridge>(response, nameof(CreateBridgeAsync), cancellationToken) ??
            await GetBridgeAsync(settings, bridgeId, nameof(CreateBridgeAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddChannelToBridgeAsync(
        string bridgeId,
        string channelId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(bridgeId);
        ArgumentException.ThrowIfNullOrEmpty(channelId);

        var settings = ResolveSettings(nameof(AddChannelToBridgeAsync));
        var query = new Dictionary<string, string>
        {
            ["channel"] = channelId,
        };
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"bridges/{Uri.EscapeDataString(bridgeId)}/addChannel",
            query,
            null,
            nameof(AddChannelToBridgeAsync),
            cancellationToken);

        await EnsureSuccessAsync(response, nameof(AddChannelToBridgeAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveChannelFromBridgeAsync(
        string bridgeId,
        string channelId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(bridgeId);
        ArgumentException.ThrowIfNullOrEmpty(channelId);

        var settings = ResolveSettings(nameof(RemoveChannelFromBridgeAsync));
        var query = new Dictionary<string, string>
        {
            ["channel"] = channelId,
        };
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"bridges/{Uri.EscapeDataString(bridgeId)}/removeChannel",
            query,
            null,
            nameof(RemoveChannelFromBridgeAsync),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessAsync(response, nameof(RemoveChannelFromBridgeAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AnswerAsync(string channelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);

        var settings = ResolveSettings(nameof(AnswerAsync));
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"channels/{Uri.EscapeDataString(channelId)}/answer",
            null,
            null,
            nameof(AnswerAsync),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        await EnsureSuccessAsync(response, nameof(AnswerAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task HangupAsync(string channelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);

        var settings = ResolveSettings(nameof(HangupAsync));
        using var response = await SendAsync(
            settings,
            HttpMethod.Delete,
            $"channels/{Uri.EscapeDataString(channelId)}",
            null,
            null,
            nameof(HangupAsync),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessAsync(response, nameof(HangupAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);

        var settings = ResolveSettings(nameof(ChannelExistsAsync));
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            $"channels/{Uri.EscapeDataString(channelId)}",
            null,
            null,
            nameof(ChannelExistsAsync),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, nameof(ChannelExistsAsync), cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(bridgeId);

        var settings = ResolveSettings(nameof(DestroyBridgeAsync));
        using var response = await SendAsync(
            settings,
            HttpMethod.Delete,
            $"bridges/{Uri.EscapeDataString(bridgeId)}",
            null,
            null,
            nameof(DestroyBridgeAsync),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessAsync(response, nameof(DestroyBridgeAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AsteriskAriLiveRecording> StartBridgeRecordingAsync(
        string bridgeId,
        string recordingName,
        string format,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(bridgeId);
        ArgumentException.ThrowIfNullOrEmpty(recordingName);
        ArgumentException.ThrowIfNullOrEmpty(format);

        var settings = ResolveSettings(nameof(StartBridgeRecordingAsync));
        var query = new Dictionary<string, string>
        {
            ["name"] = recordingName,
            ["format"] = format,
            ["ifExists"] = AsteriskAriConstants.RecordingIfExistsOverwrite,
            ["terminateOn"] = AsteriskAriConstants.RecordingTerminateOnNone,
        };
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"bridges/{Uri.EscapeDataString(bridgeId)}/record",
            query,
            null,
            nameof(StartBridgeRecordingAsync),
            cancellationToken);

        // A recording with the same deterministic name already in progress surfaces as a 409, so read the live
        // recording back and treat the start as idempotent. The caller inspects its state to resume it when paused.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return await GetLiveRecordingAsync(settings, recordingName, nameof(StartBridgeRecordingAsync), cancellationToken);
        }

        await EnsureSuccessAsync(response, nameof(StartBridgeRecordingAsync), cancellationToken);

        return await ReadJsonAsync<AsteriskAriLiveRecording>(response, nameof(StartBridgeRecordingAsync), cancellationToken) ??
            new AsteriskAriLiveRecording { Name = recordingName, Format = format };
    }

    /// <inheritdoc/>
    public async Task PauseBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordingName);

        var settings = ResolveSettings(nameof(PauseBridgeRecordingAsync));
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"recordings/live/{Uri.EscapeDataString(recordingName)}/pause",
            null,
            null,
            nameof(PauseBridgeRecordingAsync),
            cancellationToken);

        await EnsureSuccessAsync(response, nameof(PauseBridgeRecordingAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UnpauseBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordingName);

        var settings = ResolveSettings(nameof(UnpauseBridgeRecordingAsync));
        using var response = await SendAsync(
            settings,
            HttpMethod.Delete,
            $"recordings/live/{Uri.EscapeDataString(recordingName)}/pause",
            null,
            null,
            nameof(UnpauseBridgeRecordingAsync),
            cancellationToken);

        await EnsureSuccessAsync(response, nameof(UnpauseBridgeRecordingAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AsteriskAriStoredRecording> StopBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordingName);

        var settings = ResolveSettings(nameof(StopBridgeRecordingAsync));
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"recordings/live/{Uri.EscapeDataString(recordingName)}/stop",
            null,
            null,
            nameof(StopBridgeRecordingAsync),
            cancellationToken);

        // Stopping a recording that is no longer live (already stopped, or never started) is an idempotent no-op.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, nameof(StopBridgeRecordingAsync), cancellationToken);

        return await GetStoredRecordingAsync(settings, recordingName, nameof(StopBridgeRecordingAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AsteriskAriChannel> SnoopChannelAsync(
        string channelId,
        string spy,
        string whisper,
        string snoopId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);
        ArgumentException.ThrowIfNullOrEmpty(spy);
        ArgumentException.ThrowIfNullOrEmpty(whisper);
        ArgumentException.ThrowIfNullOrEmpty(snoopId);

        var settings = ResolveSettings(nameof(SnoopChannelAsync));
        var query = new Dictionary<string, string>
        {
            // The snoop channel enters the tenant's own Stasis application (resolved from validated tenant settings,
            // never from caller input) so its lifecycle events stay tenant-owned per CC-1.
            ["app"] = settings.ApplicationName,
            ["spy"] = spy,
            ["whisper"] = whisper,
            ["snoopId"] = snoopId,
        };
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"channels/{Uri.EscapeDataString(channelId)}/snoop",
            query,
            null,
            nameof(SnoopChannelAsync),
            cancellationToken);

        // A snoop with the same deterministic id already in progress surfaces as a 409, so read the existing snoop
        // channel back and treat the create as idempotent.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return await GetChannelAsync(settings, snoopId, nameof(SnoopChannelAsync), cancellationToken);
        }

        await EnsureSuccessAsync(response, nameof(SnoopChannelAsync), cancellationToken);

        return await ReadJsonAsync<AsteriskAriChannel>(response, nameof(SnoopChannelAsync), cancellationToken) ??
            await GetChannelAsync(settings, snoopId, nameof(SnoopChannelAsync), cancellationToken);
    }

    private async Task<AsteriskAriLiveRecording> GetLiveRecordingAsync(
        AsteriskResolvedSettings settings,
        string recordingName,
        string operation,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            $"recordings/live/{Uri.EscapeDataString(recordingName)}",
            null,
            null,
            operation,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new AsteriskAriLiveRecording { Name = recordingName };
        }

        await EnsureSuccessAsync(response, operation, cancellationToken);

        return await ReadJsonAsync<AsteriskAriLiveRecording>(response, operation, cancellationToken) ??
            new AsteriskAriLiveRecording { Name = recordingName };
    }

    private async Task<AsteriskAriStoredRecording> GetStoredRecordingAsync(
        AsteriskResolvedSettings settings,
        string recordingName,
        string operation,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            $"recordings/stored/{Uri.EscapeDataString(recordingName)}",
            null,
            null,
            operation,
            cancellationToken);

        // The stored file may not be readable yet (or may have been removed by retention), so a missing stored
        // recording does not fail the stop; the metadata simply omits the fields that could not be read.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, operation, cancellationToken);

        return await ReadJsonAsync<AsteriskAriStoredRecording>(response, operation, cancellationToken);
    }

    private async Task<AsteriskAriChannel> GetChannelAsync(
        AsteriskResolvedSettings settings,
        string channelId,
        string operation,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            $"channels/{Uri.EscapeDataString(channelId)}",
            null,
            null,
            operation,
            cancellationToken);
        await EnsureSuccessAsync(response, operation, cancellationToken);

        return await ReadJsonAsync<AsteriskAriChannel>(response, operation, cancellationToken) ??
            new AsteriskAriChannel { Id = channelId };
    }

    private async Task<AsteriskAriBridge> GetBridgeAsync(
        AsteriskResolvedSettings settings,
        string bridgeId,
        string operation,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            $"bridges/{Uri.EscapeDataString(bridgeId)}",
            null,
            null,
            operation,
            cancellationToken);
        await EnsureSuccessAsync(response, operation, cancellationToken);

        return await ReadJsonAsync<AsteriskAriBridge>(response, operation, cancellationToken) ??
            new AsteriskAriBridge { Id = bridgeId };
    }

    private async Task<HttpResponseMessage> SendAsync(
        AsteriskResolvedSettings settings,
        HttpMethod method,
        string relativePath,
        IDictionary<string, string> query,
        HttpContent content,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateClient(settings);
            var path = query is null
                ? relativePath
                : QueryHelpers.AddQueryString(relativePath, query);
            using var request = new HttpRequestMessage(method, path)
            {
                Content = content,
            };

            return await client.SendAsync(request, cancellationToken);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "Asterisk ARI operation {Operation} timed out before a response was observed.", operation);

            // The resilience pipeline exhausted its attempt or total-request timeout without a response, so the
            // operation may still be taking effect on Asterisk. Surface it as a null-status AsteriskAriException so the
            // ambiguity classifier retains the durable record for the age-gated reconciler instead of deleting it.
            throw new AsteriskAriException(operation, null, "Asterisk ARI timed out before a response was observed.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "Asterisk ARI operation {Operation} could not reach Asterisk.", operation);

            throw new AsteriskAriException(operation, null, "Asterisk ARI could not reach Asterisk.", ex);
        }
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogError(
            "Asterisk ARI operation {Operation} failed with status code {StatusCode}. Response: {ResponseBody}",
            operation,
            response.StatusCode,
            OperationalLogRedactor.Redact(responseBody, OperationalLogFieldKind.FreeText));

        throw new AsteriskAriException(
            operation,
            response.StatusCode,
            $"Asterisk ARI operation '{operation}' failed with status code {(int)response.StatusCode}.");
    }

    private async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "Asterisk ARI operation {Operation} returned invalid JSON.", operation);

            throw new AsteriskAriException(operation, response.StatusCode, "Asterisk ARI returned invalid JSON.", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "Asterisk ARI operation {Operation} returned unsupported JSON.", operation);

            throw new AsteriskAriException(operation, response.StatusCode, "Asterisk ARI returned unsupported JSON.", ex);
        }
    }

    private AsteriskResolvedSettings ResolveSettings(string operation)
    {
        var tenantSettings = _siteService.GetSettings<AsteriskSettings>();

        if (tenantSettings?.IsEnabled == true)
        {
            var settings = new AsteriskResolvedSettings
            {
                IsEnabled = true,
                ProviderName = AsteriskConstants.ProviderTechnicalName,
                BaseUrl = tenantSettings.BaseUrl,
                UserName = tenantSettings.UserName,
                Password = UnprotectPassword(tenantSettings.Password),
                ApplicationName = tenantSettings.ApplicationName,
                TimeoutSeconds = tenantSettings.TimeoutSeconds,
            };

            AsteriskSettingsUtilities.ApplyDefaults(settings);

            // Every ARI operation path enforces single-tenant ownership of the (BaseUrl, ApplicationName) pair through
            // the shared gate (host-default collision check plus an atomic, generation-reference-counted claim). Fail
            // closed when the tenant does not own the application so a misconfigured shared app cannot cross-deliver
            // Stasis events between tenants.
            if (!_applicationGate.TryAcquire(settings))
            {
                throw new AsteriskAriException(operation, null, "The Asterisk provider is not configured.");
            }

            return EnsureConfigured(settings, operation);
        }

        // The host-level default Asterisk connection is a single shared server credential, so only the default shell
        // may fall back to it. Allowing non-default tenants to share the same ARI application would cross-deliver
        // Stasis events between tenants, so each non-default tenant must configure its own Asterisk settings (with a
        // unique application name) or the provider stays unavailable by construction.
        if (!_shellSettings.IsDefaultShell())
        {
            throw new AsteriskAriException(operation, null, "The Asterisk provider is not configured.");
        }

        var defaultSettings = new AsteriskResolvedSettings
        {
            IsEnabled = _defaultOptions.IsEnabled,
            ProviderName = AsteriskConstants.DefaultProviderTechnicalName,
            BaseUrl = _defaultOptions.BaseUrl,
            UserName = _defaultOptions.UserName,
            Password = _defaultOptions.Password,
            ApplicationName = _defaultOptions.ApplicationName,
            TimeoutSeconds = _defaultOptions.TimeoutSeconds,
        };

        AsteriskSettingsUtilities.ApplyDefaults(defaultSettings);

        // The default shell claims the host-default application through the same gate so the process-wide registry
        // records the owner. This is idempotent with the listener's claim under the same generation token.
        if (!_applicationGate.TryAcquire(defaultSettings))
        {
            throw new AsteriskAriException(operation, null, "The Asterisk provider is not configured.");
        }

        return EnsureConfigured(defaultSettings, operation);
    }

    private static AsteriskResolvedSettings EnsureConfigured(AsteriskResolvedSettings settings, string operation)
    {
        if (AsteriskSettingsUtilities.HasRequiredConfiguration(settings))
        {
            return settings;
        }

        throw new AsteriskAriException(operation, null, "The Asterisk provider is not configured.");
    }

    private HttpClient CreateClient(AsteriskResolvedSettings settings)
    {
        var client = _httpClientFactory.CreateClient(AsteriskConstants.HttpClientName);
        client.BaseAddress = new Uri(AsteriskSettingsUtilities.NormalizeBaseUrl(settings.BaseUrl), UriKind.Absolute);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{settings.Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);

        return client;
    }

    private string UnprotectPassword(string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Unprotect(protectedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(OperationalLogRedactor.RedactException(ex), "Unable to unprotect the tenant Asterisk password for ARI.");

            return null;
        }
    }

    private static void ValidateOriginateRequest(AsteriskAriOriginateRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.Endpoint);
        ArgumentException.ThrowIfNullOrEmpty(request.ChannelId);

        if (request.AppArgs is null ||
            !request.AppArgs.Contains(AsteriskConstants.OriginationMarkerVariableName, StringComparer.Ordinal))
        {
            throw new ArgumentException("The originate request must include the Contact Center origination marker application argument.", nameof(request));
        }

        if (request.Variables is null ||
            !request.Variables.ContainsKey(AsteriskConstants.OriginationMarkerVariableName) ||
            !request.Variables.ContainsKey(AsteriskConstants.InteractionChannelVariableName))
        {
            throw new ArgumentException("The originate request must include the Contact Center origination marker and interaction channel variables.", nameof(request));
        }
    }

    private static async Task<string> ReadResponseBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private sealed class AsteriskAriVariablesPayload
    {
        [JsonPropertyName("variables")]
        public IDictionary<string, string> Variables { get; set; }
    }
}
