using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Opens bidirectional RTP media sessions for Asterisk calls through ARI External Media channels.
/// </summary>
public sealed class AsteriskContactCenterVoiceMediaProvider : IContactCenterVoiceMediaProvider
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly DefaultAsteriskOptions _defaultOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskContactCenterVoiceMediaProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read tenant Asterisk settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect the tenant password.</param>
    /// <param name="defaultOptions">The configuration-backed default Asterisk options.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger instance.</param>
    public AsteriskContactCenterVoiceMediaProvider(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<DefaultAsteriskOptions> defaultOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<AsteriskContactCenterVoiceMediaProvider> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _defaultOptions = defaultOptions.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string TechnicalName => AsteriskConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public async Task<IContactCenterVoiceMediaSession> OpenSessionAsync(
        ContactCenterVoiceMediaSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            throw new ArgumentException("A provider call id is required.", nameof(request));
        }

        ValidatePreferredFormat(request.PreferredIncomingFormat);
        ValidatePreferredFormat(request.PreferredOutgoingFormat);

        if (!request.Metadata.TryGetValue(AsteriskConstants.ExternalMediaHostMetadataKey, out var externalHost) ||
            string.IsNullOrWhiteSpace(externalHost))
        {
            throw new InvalidOperationException(
                $"Asterisk RTP media requires the '{AsteriskConstants.ExternalMediaHostMetadataKey}' metadata value that Asterisk can reach.");
        }

        var settings = ResolveSettings();

        if (!AsteriskSettingsUtilities.HasRequiredConfiguration(settings))
        {
            throw new InvalidOperationException("The Asterisk provider is not configured.");
        }

        var udpClient = BindUdpClient(request.Metadata);
        var localPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
        var externalChannelId = default(string);
        var bridgeId = default(string);
        var ownsBridge = false;

        try
        {
            bridgeId = await FindBridgeIdAsync(settings, request.ProviderCallId, cancellationToken);

            if (string.IsNullOrEmpty(bridgeId))
            {
                bridgeId = await CreateBridgeAsync(settings, cancellationToken);
                ownsBridge = true;
                await AddChannelToBridgeAsync(settings, bridgeId, request.ProviderCallId, cancellationToken);
            }

            externalChannelId = await CreateExternalMediaChannelAsync(
                settings,
                externalHost.Trim(),
                localPort,
                cancellationToken);

            await AddChannelToBridgeAsync(settings, bridgeId, externalChannelId, cancellationToken);

            var remoteAddress = await GetChannelVariableAsync(
                settings,
                externalChannelId,
                "UNICASTRTP_LOCAL_ADDRESS",
                cancellationToken);
            var remotePortText = await GetChannelVariableAsync(
                settings,
                externalChannelId,
                "UNICASTRTP_LOCAL_PORT",
                cancellationToken);

            if (!IPAddress.TryParse(remoteAddress, out var remoteIpAddress) ||
                !int.TryParse(remotePortText, NumberStyles.None, CultureInfo.InvariantCulture, out var remotePort))
            {
                throw new InvalidOperationException("Asterisk did not provide a valid RTP return endpoint.");
            }

            return new AsteriskContactCenterVoiceMediaSession(
                externalChannelId,
                request.ProviderCallId,
                udpClient,
                new IPEndPoint(remoteIpAddress, remotePort),
                token => StopSessionAsync(settings, externalChannelId, ownsBridge ? bridgeId : null, token));
        }
        catch
        {
            udpClient.Dispose();

            try
            {
                using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await StopSessionAsync(
                    settings,
                    externalChannelId,
                    ownsBridge ? bridgeId : null,
                    cleanupCancellation.Token);
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(
                    cleanupException,
                    "Unable to clean up a partially opened Asterisk external-media session.");
            }

            throw;
        }
    }

    private AsteriskResolvedSettings ResolveSettings()
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

            return settings;
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

        return defaultSettings;
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
            _logger.LogWarning(ex, "Unable to unprotect the tenant Asterisk password for external media.");

            return null;
        }
    }

    private static UdpClient BindUdpClient(IDictionary<string, string> metadata)
    {
        var address = IPAddress.Any;

        if (metadata.TryGetValue(AsteriskConstants.ExternalMediaBindAddressMetadataKey, out var bindAddress) &&
            !string.IsNullOrWhiteSpace(bindAddress) &&
            !IPAddress.TryParse(bindAddress, out address))
        {
            throw new InvalidOperationException($"The Asterisk RTP bind address '{bindAddress}' is invalid.");
        }

        var port = 0;

        if (metadata.TryGetValue(AsteriskConstants.ExternalMediaBindPortMetadataKey, out var bindPort) &&
            !string.IsNullOrWhiteSpace(bindPort) &&
            (!int.TryParse(bindPort, NumberStyles.None, CultureInfo.InvariantCulture, out port) ||
                port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort))
        {
            throw new InvalidOperationException($"The Asterisk RTP bind port '{bindPort}' is invalid.");
        }

        return new UdpClient(new IPEndPoint(address, port));
    }

    private async Task<string> FindBridgeIdAsync(
        AsteriskResolvedSettings settings,
        string providerCallId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(settings, HttpMethod.Get, "bridges", null, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        foreach (var bridge in document.RootElement.EnumerateArray())
        {
            if (bridge.TryGetProperty("channels", out var channels) &&
                channels.EnumerateArray().Any(channel =>
                    string.Equals(channel.GetString(), providerCallId, StringComparison.Ordinal)) &&
                bridge.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }

        return null;
    }

    private async Task<string> CreateBridgeAsync(
        AsteriskResolvedSettings settings,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            "bridges",
            new Dictionary<string, string>
            {
                ["type"] = "mixing",
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadIdAsync(response, cancellationToken)
            ?? throw new InvalidOperationException("Asterisk did not return an external-media bridge id.");
    }

    private async Task<string> CreateExternalMediaChannelAsync(
        AsteriskResolvedSettings settings,
        string externalHost,
        int externalPort,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            "channels/externalMedia",
            new Dictionary<string, string>
            {
                ["app"] = settings.ApplicationName,
                ["external_host"] = $"{externalHost}:{externalPort.ToString(CultureInfo.InvariantCulture)}",
                ["format"] = "ulaw",
                ["encapsulation"] = "rtp",
                ["transport"] = "udp",
                ["connection_type"] = "client",
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadIdAsync(response, cancellationToken)
            ?? throw new InvalidOperationException("Asterisk did not return an external-media channel id.");
    }

    private async Task AddChannelToBridgeAsync(
        AsteriskResolvedSettings settings,
        string bridgeId,
        string channelId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"bridges/{Uri.EscapeDataString(bridgeId)}/addChannel",
            new Dictionary<string, string>
            {
                ["channel"] = channelId,
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task<string> GetChannelVariableAsync(
        AsteriskResolvedSettings settings,
        string channelId,
        string variableName,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            $"channels/{Uri.EscapeDataString(channelId)}/variable",
            new Dictionary<string, string>
            {
                ["variable"] = variableName,
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return document.RootElement.TryGetProperty("value", out var value)
            ? value.GetString()
            : null;
    }

    private async Task StopSessionAsync(
        AsteriskResolvedSettings settings,
        string externalChannelId,
        string ownedBridgeId,
        CancellationToken cancellationToken)
    {
        List<Exception> cleanupExceptions = [];

        if (!string.IsNullOrWhiteSpace(externalChannelId))
        {
            try
            {
                using var channelResponse = await SendAsync(
                    settings,
                    HttpMethod.Delete,
                    $"channels/{Uri.EscapeDataString(externalChannelId)}",
                    null,
                    cancellationToken);

                EnsureCleanupSucceeded(channelResponse);
            }
            catch (Exception exception)
            {
                cleanupExceptions.Add(exception);
            }
        }

        if (!string.IsNullOrWhiteSpace(ownedBridgeId))
        {
            try
            {
                using var bridgeResponse = await SendAsync(
                    settings,
                    HttpMethod.Delete,
                    $"bridges/{Uri.EscapeDataString(ownedBridgeId)}",
                    null,
                    cancellationToken);

                EnsureCleanupSucceeded(bridgeResponse);
            }
            catch (Exception exception)
            {
                cleanupExceptions.Add(exception);
            }
        }

        if (cleanupExceptions.Count > 0)
        {
            throw new AggregateException("Unable to clean up the Asterisk external-media session.", cleanupExceptions);
        }
    }

    private static void EnsureCleanupSucceeded(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAsync(
        AsteriskResolvedSettings settings,
        HttpMethod method,
        string relativePath,
        IDictionary<string, string> query,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(AsteriskConstants.HttpClientName);
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{settings.Password}")));

        var path = query is null
            ? relativePath
            : QueryHelpers.AddQueryString(relativePath, query);
        var request = new HttpRequestMessage(method, path);

        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<string> ReadIdAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return document.RootElement.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    private static void ValidatePreferredFormat(ContactCenterVoiceMediaFormat format)
    {
        if (format is null)
        {
            return;
        }

        if (format.Encoding is not ContactCenterVoiceMediaEncoding.Unknown and
            not ContactCenterVoiceMediaEncoding.MuLaw)
        {
            throw new NotSupportedException("The Asterisk RTP media adapter currently supports only G.711 mu-law audio.");
        }

        if (format.SampleRate is not 0 and not 8_000)
        {
            throw new NotSupportedException("The Asterisk RTP media adapter currently supports only an 8 kHz sample rate.");
        }

        if (format.Channels is not 0 and not 1)
        {
            throw new NotSupportedException("The Asterisk RTP media adapter currently supports only mono audio.");
        }
    }
}
