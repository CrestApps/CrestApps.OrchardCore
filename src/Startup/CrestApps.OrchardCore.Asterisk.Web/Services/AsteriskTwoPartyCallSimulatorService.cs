using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.OrchardCore.Asterisk.Web.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Creates two Stasis-managed Asterisk channels and joins them through a mixing bridge.
/// </summary>
public sealed class AsteriskTwoPartyCallSimulatorService
{
    private static readonly TimeSpan _channelReadyPollingInterval = TimeSpan.FromMilliseconds(250);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AsteriskDashboardBroadcastService _dashboardBroadcastService;
    private readonly AsteriskWebOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskTwoPartyCallSimulatorService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="dashboardBroadcastService">The dashboard refresh coordinator.</param>
    /// <param name="options">The configured Asterisk options.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public AsteriskTwoPartyCallSimulatorService(
        IHttpClientFactory httpClientFactory,
        AsteriskDashboardBroadcastService dashboardBroadcastService,
        IOptions<AsteriskWebOptions> options,
        TimeProvider timeProvider,
        ILogger<AsteriskTwoPartyCallSimulatorService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _dashboardBroadcastService = dashboardBroadcastService;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Originates two endpoints and connects their Stasis channels through an Asterisk mixing bridge.
    /// </summary>
    /// <param name="input">The endpoint and caller-id selections.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created channel and bridge identifiers.</returns>
    public async Task<TwoPartyCallSimulationResult> SimulateAsync(
        TwoPartyCallSimulationInputModel input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!AsteriskAriConnectionUtilities.IsConfigured(_options) ||
            string.IsNullOrWhiteSpace(_options.AsteriskApplicationName))
        {
            throw new InvalidOperationException("The two-party simulator is missing the Asterisk ARI connection or Stasis application settings.");
        }

        if (string.IsNullOrWhiteSpace(input.PartyAEndpoint) ||
            string.IsNullOrWhiteSpace(input.PartyBEndpoint))
        {
            throw new ArgumentException("Both Asterisk party endpoints are required.", nameof(input));
        }

        var simulationId = Guid.NewGuid().ToString("N");
        string partyAChannelId = null;
        string partyBChannelId = null;
        string bridgeId = null;

        try
        {
            async Task OriginatePartyAAsync()
            {
                partyAChannelId = await OriginateAsync(
                    input.PartyAEndpoint,
                    input.PartyACallerId,
                    simulationId,
                    "party-a",
                    cancellationToken);
            }

            async Task OriginatePartyBAsync()
            {
                partyBChannelId = await OriginateAsync(
                    input.PartyBEndpoint,
                    input.PartyBCallerId,
                    simulationId,
                    "party-b",
                    cancellationToken);
            }

            await Task.WhenAll(
                OriginatePartyAAsync(),
                OriginatePartyBAsync());

            await Task.WhenAll(
                WaitForStasisAsync(partyAChannelId, simulationId, cancellationToken),
                WaitForStasisAsync(partyBChannelId, simulationId, cancellationToken));

            bridgeId = await CreateBridgeAsync(simulationId, cancellationToken);
            await AddChannelsToBridgeAsync(
                bridgeId,
                partyAChannelId,
                partyBChannelId,
                cancellationToken);

            _dashboardBroadcastService.RequestRefresh("two-party simulation created");

            return new TwoPartyCallSimulationResult
            {
                SimulationId = simulationId,
                BridgeId = bridgeId,
                PartyAChannelId = partyAChannelId,
                PartyBChannelId = partyBChannelId,
            };
        }
        catch
        {
            using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await CleanupAsync(
                partyAChannelId,
                partyBChannelId,
                bridgeId,
                cleanupCancellation.Token);

            throw;
        }
    }

    private async Task<string> OriginateAsync(
        string endpoint,
        string callerId,
        string simulationId,
        string role,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>
        {
            ["endpoint"] = endpoint.Trim(),
            ["timeout"] = Math.Max(1, _options.AsteriskTimeoutSeconds).ToString(CultureInfo.InvariantCulture),
            ["app"] = _options.AsteriskApplicationName.Trim(),
            ["appArgs"] = $"two-party:{simulationId}:{role}",
        };

        if (!string.IsNullOrWhiteSpace(callerId))
        {
            query["callerId"] = callerId.Trim();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, QueryHelpers.AddQueryString("channels", query))
        {
            Content = JsonContent.Create(new
            {
                variables = new Dictionary<string, string>
                {
                    ["CRESTAPPS_SIMULATION_ID"] = simulationId,
                    ["CRESTAPPS_SIMULATION_ROLE"] = role,
                },
            }),
        };
        using var response = await SendAsync(request, cancellationToken);
        var rawResponse = await EnsureSuccessAsync(response, "originate a simulation party", cancellationToken);

        return ReadRequiredId(rawResponse, "Asterisk did not return an originated channel id.");
    }

    private async Task WaitForStasisAsync(
        string channelId,
        string simulationId,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(5, _options.AsteriskTimeoutSeconds));
        var deadline = _timeProvider.GetUtcNow().Add(timeout);

        while (_timeProvider.GetUtcNow() < deadline)
        {
            await Task.Delay(_channelReadyPollingInterval, cancellationToken);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"channels/{Uri.EscapeDataString(channelId)}");
            using var response = await SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Asterisk channel '{channelId}' ended before it entered Stasis.");
            }

            var rawResponse = await EnsureSuccessAsync(response, "query an originated simulation party", cancellationToken);

            if (IsExpectedStasisChannel(rawResponse, simulationId))
            {
                return;
            }
        }

        throw new TimeoutException($"Asterisk channel '{channelId}' did not enter the configured Stasis application within {timeout.TotalSeconds:0} seconds.");
    }

    private async Task<string> CreateBridgeAsync(
        string simulationId,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>
        {
            ["type"] = "mixing",
            ["name"] = $"crestapps-two-party-{simulationId}",
        };
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            QueryHelpers.AddQueryString("bridges", query));
        using var response = await SendAsync(request, cancellationToken);
        var rawResponse = await EnsureSuccessAsync(response, "create a simulation bridge", cancellationToken);

        return ReadRequiredId(rawResponse, "Asterisk did not return a simulation bridge id.");
    }

    private async Task AddChannelsToBridgeAsync(
        string bridgeId,
        string partyAChannelId,
        string partyBChannelId,
        CancellationToken cancellationToken)
    {
        var path = QueryHelpers.AddQueryString(
            $"bridges/{Uri.EscapeDataString(bridgeId)}/addChannel",
            "channel",
            $"{partyAChannelId},{partyBChannelId}");
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "add both simulation parties to the bridge", cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        AsteriskAriConnectionUtilities.ApplyBasicAuthentication(client, _options);

        return await client.SendAsync(request, cancellationToken);
    }

    private bool IsExpectedStasisChannel(string rawResponse, string simulationId)
    {
        using var document = JsonDocument.Parse(rawResponse);

        if (!document.RootElement.TryGetProperty("dialplan", out var dialplan) ||
            !dialplan.TryGetProperty("app_name", out var applicationName) ||
            !string.Equals(applicationName.GetString(), "Stasis", StringComparison.OrdinalIgnoreCase) ||
            !dialplan.TryGetProperty("app_data", out var applicationData))
        {
            return false;
        }

        var arguments = applicationData.GetString()?.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return arguments is not null &&
            arguments.Contains(_options.AsteriskApplicationName, StringComparer.Ordinal) &&
            arguments.Any(argument => argument.StartsWith(
                $"two-party:{simulationId}:",
                StringComparison.Ordinal));
    }

    private async Task CleanupAsync(
        string partyAChannelId,
        string partyBChannelId,
        string bridgeId,
        CancellationToken cancellationToken)
    {
        foreach (var channelId in new[] { partyAChannelId, partyBChannelId })
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                continue;
            }

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"channels/{Uri.EscapeDataString(channelId)}");
                using var response = await SendAsync(request, cancellationToken);
                LogCleanupFailure(response, "channel", channelId);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to clean up Asterisk simulation channel {ChannelId}.",
                    channelId);
            }
        }

        if (!string.IsNullOrWhiteSpace(bridgeId))
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"bridges/{Uri.EscapeDataString(bridgeId)}");
                using var response = await SendAsync(request, cancellationToken);
                LogCleanupFailure(response, "bridge", bridgeId);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to clean up Asterisk simulation bridge {BridgeId}.",
                    bridgeId);
            }
        }
    }

    private void LogCleanupFailure(
        HttpResponseMessage response,
        string resourceType,
        string resourceId)
    {
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        _logger.LogWarning(
            "Asterisk returned {StatusCode} while cleaning up simulation {ResourceType} {ResourceId}.",
            (int)response.StatusCode,
            resourceType,
            resourceId);
    }

    private static async Task<string> EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return rawResponse;
        }

        var detail = string.IsNullOrWhiteSpace(rawResponse)
            ? response.ReasonPhrase
            : rawResponse;

        throw new InvalidOperationException($"Asterisk could not {operation}: {(int)response.StatusCode} {detail}");
    }

    private static string ReadRequiredId(string rawResponse, string errorMessage)
    {
        using var document = JsonDocument.Parse(rawResponse);

        if (document.RootElement.TryGetProperty("id", out var id) &&
            !string.IsNullOrWhiteSpace(id.GetString()))
        {
            return id.GetString();
        }

        throw new InvalidOperationException(errorMessage);
    }
}
