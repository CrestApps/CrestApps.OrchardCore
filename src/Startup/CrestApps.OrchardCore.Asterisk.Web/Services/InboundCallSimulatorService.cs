using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.OrchardCore.Asterisk.Web.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Signs in to Orchard Core, originates one or more Asterisk Stasis channels, and waits for the
/// background ARI listener to forward the matching inbound events into Contact Center.
/// </summary>
public sealed class InboundCallSimulatorService
{
    private readonly OrchardSignInClient _signInClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AsteriskInboundSimulationCoordinator _coordinator;
    private readonly AsteriskWebOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundCallSimulatorService"/> class.
    /// </summary>
    /// <param name="signInClient">The sign-in client.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="coordinator">The Stasis simulation coordinator.</param>
    /// <param name="options">The configured sample app options.</param>
    /// <param name="timeProvider">The time provider.</param>
    public InboundCallSimulatorService(
        OrchardSignInClient signInClient,
        IHttpClientFactory httpClientFactory,
        AsteriskInboundSimulationCoordinator coordinator,
        IOptions<AsteriskWebOptions> options,
        TimeProvider timeProvider)
    {
        _signInClient = signInClient;
        _httpClientFactory = httpClientFactory;
        _coordinator = coordinator;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Simulates one burst of inbound calls against the configured Orchard site.
    /// </summary>
    /// <param name="input">The simulator input.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The per-call results.</returns>
    public async Task<IReadOnlyList<InboundCallSimulationResult>> SimulateAsync(
        InboundCallSimulationInputModel input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!AsteriskAriConnectionUtilities.IsConfigured(_options) ||
            string.IsNullOrWhiteSpace(_options.AsteriskApplicationName))
        {
            throw new InvalidOperationException("The simulator is missing the Asterisk ARI connection or Stasis application settings.");
        }

        var authenticatedClient = await _signInClient.CreateAuthenticatedClientAsync(input, cancellationToken);
        using var client = authenticatedClient.Client;
        var ingressUri = new Uri(client.BaseAddress!, input.InboundPath.TrimStart('/'));
        var batchId = _timeProvider.GetUtcNow().ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

        if (!input.SendConcurrently)
        {
            var sequentialResults = new List<InboundCallSimulationResult>(input.Count);

            for (var index = 0; index < input.Count; index++)
            {
                sequentialResults.Add(await SendAsync(
                    client,
                    authenticatedClient.RequestVerificationToken,
                    ingressUri,
                    input,
                    batchId,
                    index,
                    cancellationToken));
            }

            return sequentialResults;
        }

        var tasks = Enumerable
            .Range(0, input.Count)
            .Select(index => SendAsync(
                client,
                authenticatedClient.RequestVerificationToken,
                ingressUri,
                input,
                batchId,
                index,
                cancellationToken));

        return await Task.WhenAll(tasks);
    }

    private async Task<InboundCallSimulationResult> SendAsync(
        HttpClient client,
        string requestVerificationToken,
        Uri ingressUri,
        InboundCallSimulationInputModel input,
        string batchId,
        int index,
        CancellationToken cancellationToken)
    {
        var callerNumber = GenerateCallerNumber(input.CallerNumberSeed, index);
        var callerName = input.Count == 1
            ? input.CallerNamePrefix
            : $"{input.CallerNamePrefix} {index + 1}";
        var simulationKey = _coordinator.Register(
            client,
            requestVerificationToken,
            ingressUri,
            input.ProviderName,
            input.ToAddress,
            batchId,
            index,
            callerNumber,
            callerName,
            input.AsteriskDestination.Trim());

        var origination = await OriginateAsteriskAsync(input, callerNumber, callerName, simulationKey, cancellationToken);

        if (!origination.Succeeded)
        {
            _coordinator.Cancel(simulationKey);

            return new InboundCallSimulationResult
            {
                ProviderCallId = $"sim-{batchId}-{index + 1:D3}",
                CallerNumber = callerNumber,
                CallerName = callerName,
                Succeeded = false,
                StatusCode = origination.StatusCode,
                RawResponse = origination.ErrorMessage,
            };
        }

        _coordinator.SetOriginatedChannel(simulationKey, origination.ChannelId);

        return await _coordinator.WaitForCompletionAsync(
            simulationKey,
            TimeSpan.FromSeconds(Math.Max(10, _options.SimulationTimeoutSeconds)),
            cancellationToken);
    }

    private async Task<AsteriskOriginationOutcome> OriginateAsteriskAsync(
        InboundCallSimulationInputModel input,
        string callerNumber,
        string callerName,
        string simulationKey,
        CancellationToken cancellationToken)
    {
        var destination = input.AsteriskDestination.Trim();
        var endpointTemplate = string.IsNullOrWhiteSpace(_options.AsteriskEndpointTemplate)
            ? "Local/{number}@default"
            : _options.AsteriskEndpointTemplate.Trim();
        var endpoint = endpointTemplate.Replace("{number}", destination, StringComparison.OrdinalIgnoreCase);
        var query = new Dictionary<string, string>
        {
            ["endpoint"] = endpoint,
            ["timeout"] = Math.Max(1, _options.AsteriskTimeoutSeconds).ToString(CultureInfo.InvariantCulture),
            ["app"] = _options.AsteriskApplicationName.Trim(),
            ["appArgs"] = $"sim:{simulationKey}",
        };

        var callerId = string.IsNullOrWhiteSpace(callerName)
            ? callerNumber
            : $"{callerName} <{callerNumber}>";

        if (!string.IsNullOrWhiteSpace(callerId))
        {
            query["callerId"] = callerId;
        }

        var variables = new Dictionary<string, string>
        {
            ["SIMULATION_KEY"] = simulationKey,
            ["SIMULATED_INBOUND"] = "true",
            ["SIMULATED_TO"] = input.ToAddress,
            ["SIMULATED_FROM"] = callerNumber,
        };

        var client = _httpClientFactory.CreateClient();
        AsteriskAriConnectionUtilities.ApplyBasicAuthentication(client, _options);

        using var request = new HttpRequestMessage(HttpMethod.Post, QueryHelpers.AddQueryString("channels", query))
        {
            Content = JsonContent.Create(new { variables }),
        };
        using var response = await client.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new AsteriskOriginationOutcome
            {
                ErrorMessage = string.IsNullOrWhiteSpace(rawResponse)
                    ? $"Asterisk rejected the originate request with status code {(int)response.StatusCode}."
                    : rawResponse,
                StatusCode = (int)response.StatusCode,
            };
        }

        return new AsteriskOriginationOutcome
        {
            Succeeded = true,
            ChannelId = TryReadId(rawResponse),
            StatusCode = (int)response.StatusCode,
        };
    }

    private static string TryReadId(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawResponse);

            return document.RootElement.TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GenerateCallerNumber(string seed, int index)
    {
        if (index == 0)
        {
            return seed;
        }

        var hasPlus = seed.StartsWith('+');
        var digits = new string(seed.Where(char.IsDigit).ToArray());

        if (!long.TryParse(digits, out var numericValue))
        {
            return $"{seed}-{index + 1}";
        }

        var updated = (numericValue + index).ToString(CultureInfo.InvariantCulture).PadLeft(digits.Length, '0');

        return hasPlus ? $"+{updated}" : updated;
    }

    private sealed class AsteriskOriginationOutcome
    {
        public bool Succeeded { get; set; }

        public string ChannelId { get; set; }

        public string ErrorMessage { get; set; }

        public int StatusCode { get; set; }
    }
}
