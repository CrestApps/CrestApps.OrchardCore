using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.OrchardCore.Asterisk.Web.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Coordinates Stasis-driven inbound simulations between the Razor page request and the background
/// ARI event listener.
/// </summary>
public sealed class AsteriskInboundSimulationCoordinator
{
    private const string RequestVerificationTokenHeaderName = "RequestVerificationToken";

    private readonly ConcurrentDictionary<string, PendingSimulation> _pending = new(StringComparer.Ordinal);
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskInboundSimulationCoordinator"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public AsteriskInboundSimulationCoordinator(ILogger<AsteriskInboundSimulationCoordinator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a pending simulation and returns the key the ARI listener uses to match the Stasis event.
    /// </summary>
    /// <param name="client">The authenticated Orchard client.</param>
    /// <param name="requestVerificationToken">The Orchard anti-forgery token, when present.</param>
    /// <param name="ingressUri">The Contact Center ingress URI.</param>
    /// <param name="providerName">The provider name stamped onto the normalized inbound event.</param>
    /// <param name="toAddress">The dialed service address or DID.</param>
    /// <param name="batchId">The current simulation batch identifier.</param>
    /// <param name="index">The zero-based call index within the batch.</param>
    /// <param name="callerNumber">The generated caller number.</param>
    /// <param name="callerName">The generated caller display name.</param>
    /// <param name="asteriskDestination">The Asterisk destination that was originated.</param>
    /// <returns>The correlation key used in the ARI Stasis app args.</returns>
    public string Register(
        HttpClient client,
        string requestVerificationToken,
        Uri ingressUri,
        string providerName,
        string toAddress,
        string batchId,
        int index,
        string callerNumber,
        string callerName,
        string asteriskDestination)
    {
        var key = Guid.NewGuid().ToString("N");
        _pending[key] = new PendingSimulation
        {
            Client = client,
            RequestVerificationToken = requestVerificationToken,
            IngressUri = ingressUri,
            ProviderName = providerName,
            ToAddress = toAddress,
            BatchId = batchId,
            Index = index,
            CallerNumber = callerNumber,
            CallerName = callerName,
            AsteriskDestination = asteriskDestination,
        };

        return key;
    }

    /// <summary>
    /// Records the Asterisk channel created for a pending simulation.
    /// </summary>
    /// <param name="key">The simulation key.</param>
    /// <param name="channelId">The originated Asterisk channel identifier.</param>
    public void SetOriginatedChannel(string key, string channelId)
    {
        if (_pending.TryGetValue(key, out var pending))
        {
            pending.AsteriskOriginated = true;
            pending.AsteriskChannelId = channelId;
        }
    }

    /// <summary>
    /// Completes a pending simulation when the ARI listener receives the matching Stasis event.
    /// </summary>
    /// <param name="key">The simulation key.</param>
    /// <param name="channelId">The Asterisk channel identifier from the Stasis event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the key matched a pending simulation; otherwise <see langword="false"/>.</returns>
    public async Task<bool> TryDispatchAsync(string key, string channelId, CancellationToken cancellationToken)
    {
        if (!_pending.TryGetValue(key, out var pending) || !pending.TryBeginDispatch())
        {
            return false;
        }

        pending.AsteriskOriginated = true;
        pending.AsteriskChannelId = channelId;

        try
        {
            pending.Completion.TrySetResult(await ForwardToOrchardAsync(pending, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The Asterisk Stasis listener failed while forwarding simulation {SimulationKey} to Orchard.", key);
            pending.Completion.TrySetResult(BuildFailureResult(
                pending,
                "The Stasis listener could not forward the call to Orchard.",
                500));
        }

        return true;
    }

    /// <summary>
    /// Cancels a pending simulation because the originate request failed before Asterisk created a channel.
    /// </summary>
    /// <param name="key">The simulation key.</param>
    public void Cancel(string key)
    {
        _pending.TryRemove(key, out _);
    }

    /// <summary>
    /// Waits for the Stasis listener to forward the simulation to Orchard.
    /// </summary>
    /// <param name="key">The simulation key.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed inbound-call simulation result.</returns>
    public async Task<InboundCallSimulationResult> WaitForCompletionAsync(
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!_pending.TryGetValue(key, out var pending))
        {
            return new InboundCallSimulationResult
            {
                Succeeded = false,
                StatusCode = 500,
                RawResponse = "The pending Asterisk simulation could not be found.",
            };
        }

        try
        {
            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delayTask = Task.Delay(timeout, timeoutCancellation.Token);
            var completedTask = await Task.WhenAny(pending.Completion.Task, delayTask);

            if (completedTask == delayTask)
            {
                pending.Completion.TrySetResult(BuildFailureResult(
                    pending,
                    "Timed out waiting for the Asterisk Stasis event to reach Orchard.",
                    504));
            }
            else
            {
                timeoutCancellation.Cancel();
            }

            return await pending.Completion.Task;
        }
        finally
        {
            _pending.TryRemove(key, out _);
        }
    }

    private static async Task<InboundCallSimulationResult> ForwardToOrchardAsync(
        PendingSimulation pending,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var request = new HttpRequestMessage(HttpMethod.Post, pending.IngressUri)
        {
            Content = JsonContent.Create(new
            {
                providerName = pending.ProviderName,
                providerCallId = pending.AsteriskChannelId,
                fromAddress = pending.CallerNumber,
                toAddress = pending.ToAddress,
                callerName = pending.CallerName,
                metadata = BuildMetadata(pending),
            }),
        };

        if (!string.IsNullOrWhiteSpace(pending.RequestVerificationToken))
        {
            request.Headers.TryAddWithoutValidation(RequestVerificationTokenHeaderName, pending.RequestVerificationToken);
        }

        using var response = await pending.Client.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        var result = new InboundCallSimulationResult
        {
            ProviderCallId = pending.AsteriskChannelId,
            AsteriskOriginated = pending.AsteriskOriginated,
            AsteriskChannelId = pending.AsteriskChannelId,
            CallerNumber = pending.CallerNumber,
            CallerName = pending.CallerName,
            Succeeded = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds,
            RawResponse = rawResponse,
        };

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(rawResponse);
            var root = document.RootElement;

            if (root.TryGetProperty("routed", out var routed))
            {
                result.Routed = routed.GetBoolean();
            }

            if (root.TryGetProperty("interactionId", out var interactionId))
            {
                result.InteractionId = interactionId.GetString();
            }

            if (root.TryGetProperty("activityItemId", out var activityItemId))
            {
                result.ActivityItemId = activityItemId.GetString();
            }

            if (root.TryGetProperty("queueId", out var queueId))
            {
                result.QueueId = queueId.GetString();
            }

            if (root.TryGetProperty("agentUserId", out var agentUserId))
            {
                result.AgentUserId = agentUserId.GetString();
            }

            if (root.TryGetProperty("reason", out var reason))
            {
                result.Reason = reason.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static Dictionary<string, string> BuildMetadata(PendingSimulation pending)
    {
        return new Dictionary<string, string>
        {
            ["source"] = "CrestApps.OrchardCore.Asterisk.Web",
            ["batchId"] = pending.BatchId,
            ["requestIndex"] = (pending.Index + 1).ToString(CultureInfo.InvariantCulture),
            ["simulationMode"] = "asterisk-stasis",
            ["asteriskChannelId"] = pending.AsteriskChannelId,
            ["asteriskDestination"] = pending.AsteriskDestination,
        };
    }

    private static InboundCallSimulationResult BuildFailureResult(PendingSimulation pending, string message, int statusCode)
    {
        return new InboundCallSimulationResult
        {
            ProviderCallId = pending.AsteriskChannelId,
            AsteriskOriginated = pending.AsteriskOriginated,
            AsteriskChannelId = pending.AsteriskChannelId,
            CallerNumber = pending.CallerNumber,
            CallerName = pending.CallerName,
            Succeeded = false,
            StatusCode = statusCode,
            RawResponse = message,
        };
    }

    private sealed class PendingSimulation
    {
        private int _dispatchStarted;

        public HttpClient Client { get; init; }

        public string RequestVerificationToken { get; init; }

        public Uri IngressUri { get; init; }

        public string ProviderName { get; init; }

        public string ToAddress { get; init; }

        public string BatchId { get; init; }

        public int Index { get; init; }

        public string CallerNumber { get; init; }

        public string CallerName { get; init; }

        public string AsteriskDestination { get; init; }

        public bool AsteriskOriginated { get; set; }

        public string AsteriskChannelId { get; set; }

        public TaskCompletionSource<InboundCallSimulationResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryBeginDispatch()
            => Interlocked.CompareExchange(ref _dispatchStarted, 1, 0) == 0;
    }
}
