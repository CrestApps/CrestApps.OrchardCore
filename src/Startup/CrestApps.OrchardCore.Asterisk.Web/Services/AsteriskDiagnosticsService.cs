using System.Text.Json;
using CrestApps.OrchardCore.Asterisk.Web.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Reads and caches a live Asterisk ARI dashboard snapshot for local development.
/// </summary>
public sealed class AsteriskDiagnosticsService
{
    private const string HoldStateVariableName = "CRESTAPPS_STATE_ONHOLD";
    private const string MuteStateVariableName = "CRESTAPPS_STATE_MUTED";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AsteriskWebOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    private AsteriskDiagnosticsSnapshot _currentSnapshot = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskDiagnosticsService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="options">The configured sample app options.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public AsteriskDiagnosticsService(
        IHttpClientFactory httpClientFactory,
        IOptions<AsteriskWebOptions> options,
        TimeProvider timeProvider,
        ILogger<AsteriskDiagnosticsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the polling interval used by the live dashboard broadcaster.
    /// </summary>
    public int RefreshSeconds => Math.Max(1, _options.AsteriskRefreshSeconds);

    /// <summary>
    /// Returns the latest cached snapshot, or refreshes it when none is available yet.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The latest dashboard snapshot.</returns>
    public async Task<AsteriskDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_currentSnapshot.LastUpdatedUtc == default)
        {
            return await RefreshAsync(cancellationToken);
        }

        return _currentSnapshot;
    }

    /// <summary>
    /// Refreshes the live dashboard snapshot from Asterisk.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The refreshed dashboard snapshot.</returns>
    public async Task<AsteriskDiagnosticsSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        var waitStartedTimestamp = _timeProvider.GetTimestamp();
        await _refreshLock.WaitAsync(cancellationToken);
        var lockWaitElapsed = _timeProvider.GetElapsedTime(waitStartedTimestamp);
        var refreshStartedTimestamp = _timeProvider.GetTimestamp();

        try
        {
            _currentSnapshot = await LoadSnapshotAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Refreshed Asterisk diagnostics in {RefreshMilliseconds} ms after waiting {LockWaitMilliseconds} ms for the refresh lock. Reachable: {Reachable}. Channels: {ChannelCount}. Bridges: {BridgeCount}. Calls: {CallCount}. Error: {ErrorMessage}",
                    _timeProvider.GetElapsedTime(refreshStartedTimestamp).TotalMilliseconds,
                    lockWaitElapsed.TotalMilliseconds,
                    _currentSnapshot.Reachable,
                    _currentSnapshot.ChannelCount,
                    _currentSnapshot.BridgeCount,
                    _currentSnapshot.ActiveCallCount,
                    _currentSnapshot.ErrorMessage);
            }

            return _currentSnapshot;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<AsteriskDiagnosticsSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = new AsteriskDiagnosticsSnapshot
        {
            BaseUrl = _options.AsteriskBaseUrl,
            LastUpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime,
        };

        if (!AsteriskAriConnectionUtilities.IsConfigured(_options))
        {
            snapshot.ErrorMessage = "Asterisk diagnostics are not configured.";

            return snapshot;
        }

        var client = _httpClientFactory.CreateClient(nameof(AsteriskDiagnosticsService));
        AsteriskAriConnectionUtilities.ApplyBasicAuthentication(client, _options);

        try
        {
            var infoTask = ReadJsonAsync(client, "asterisk/info", cancellationToken);
            var channelsTask = ReadChannelsAsync(client, cancellationToken);
            var bridgesTask = ReadBridgesAsync(client, cancellationToken);

            await Task.WhenAll(infoTask, channelsTask, bridgesTask);

            snapshot.InfoJson = await infoTask;
            (snapshot.ChannelsJson, snapshot.Channels) = await channelsTask;
            (snapshot.BridgesJson, snapshot.Bridges) = await bridgesTask;
            await EnrichChannelsAsync(client, snapshot.Channels, snapshot.Bridges, cancellationToken);
            snapshot.Calls = BuildCalls(snapshot.Channels, snapshot.Bridges);
            snapshot.ChannelCount = snapshot.Channels.Count;
            snapshot.BridgeCount = snapshot.Bridges.Count;
            snapshot.RingingChannelCount = snapshot.Channels.Count(channel => IsRingingState(channel.State));
            snapshot.ConnectedChannelCount = snapshot.Channels.Count(channel => string.Equals(channel.State, "Up", StringComparison.OrdinalIgnoreCase));
            snapshot.ActiveCallCount = snapshot.Calls.Count;
            snapshot.OldestChannelSeconds = snapshot.Channels.Count == 0
                ? 0
                : snapshot.Channels.Max(channel => channel.DurationSeconds);
            snapshot.Reachable = true;
        }
        catch (HttpRequestException ex)
        {
            snapshot.ErrorMessage = ex.Message;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            snapshot.ErrorMessage = ex.Message;
        }
        catch (UriFormatException ex)
        {
            snapshot.ErrorMessage = ex.Message;
        }
        catch (JsonException ex)
        {
            snapshot.ErrorMessage = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            snapshot.ErrorMessage = ex.Message;
        }

        return snapshot;
    }

    private static bool IsRingingState(string state)
    {
        return string.Equals(state, "Ring", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Ringing", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadJsonAsync(HttpClient client, string relativePath, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(relativePath, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return FormatJson(content, "{}");
    }

    private async Task<(string Json, IList<AsteriskChannelSnapshot> Channels)> ReadChannelsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("channels", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);
        var snapshots = new List<AsteriskChannelSnapshot>();

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var createdUtc = TryGetDateTime(item, "creationtime");
                snapshots.Add(new AsteriskChannelSnapshot
                {
                    Id = GetString(item, "id"),
                    Name = GetString(item, "name"),
                    State = GetString(item, "state"),
                    CallerNumber = GetNestedString(item, "caller", "number"),
                    CallerName = GetNestedString(item, "caller", "name"),
                    ConnectedNumber = GetNestedString(item, "connected", "number"),
                    Application = GetString(item, "dialplan", "app_name"),
                    LogicalCallKey = GetLogicalCallKey(GetString(item, "name"), GetString(item, "id")),
                    Direction = InferLegDirection(
                        GetString(item, "name"),
                        GetNestedString(item, "caller", "number"),
                        GetNestedString(item, "connected", "number")),
                    CreatedUtc = createdUtc,
                    DurationSeconds = GetDurationSeconds(createdUtc),
                });
            }
        }

        return (FormatJson(content, "[]"), snapshots.OrderByDescending(channel => channel.DurationSeconds).ToList());
    }

    private async Task<(string Json, IList<AsteriskBridgeSnapshot> Bridges)> ReadBridgesAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("bridges", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);
        var snapshots = new List<AsteriskBridgeSnapshot>();

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var createdUtc = TryGetDateTime(item, "creationtime");
                var channelIds = item.TryGetProperty("channels", out var channels) && channels.ValueKind == JsonValueKind.Array
                    ? channels.EnumerateArray()
                        .Select(channel => channel.ValueKind == JsonValueKind.String ? channel.GetString() : channel.ToString())
                        .Where(channelId => !string.IsNullOrWhiteSpace(channelId))
                        .ToList()
                    : [];

                snapshots.Add(new AsteriskBridgeSnapshot
                {
                    Id = GetString(item, "id"),
                    Name = GetString(item, "name"),
                    BridgeType = GetString(item, "bridge_type"),
                    ChannelCount = channelIds.Count,
                    ChannelIds = channelIds,
                    CreatedUtc = createdUtc,
                    DurationSeconds = GetDurationSeconds(createdUtc),
                });
            }
        }

        return (FormatJson(content, "[]"), snapshots.OrderByDescending(bridge => bridge.DurationSeconds).ToList());
    }

    private int GetDurationSeconds(DateTime? createdUtc)
    {
        if (!createdUtc.HasValue)
        {
            return 0;
        }

        return (int)Math.Max(0, (_timeProvider.GetUtcNow().UtcDateTime - createdUtc.Value).TotalSeconds);
    }

    private static DateTime? TryGetDateTime(JsonElement item, string propertyName)
    {
        var value = GetString(item, propertyName);

        if (string.IsNullOrWhiteSpace(value) || !DateTime.TryParse(value, out var parsed))
        {
            return null;
        }

        return parsed.Kind == DateTimeKind.Utc
            ? parsed
            : parsed.ToUniversalTime();
    }

    private static List<AsteriskCallSnapshot> BuildCalls(
        IList<AsteriskChannelSnapshot> channels,
        IList<AsteriskBridgeSnapshot> bridges)
    {
        var bridgeLookup = bridges
            .Where(bridge => !string.IsNullOrWhiteSpace(bridge.Id))
            .ToDictionary(bridge => bridge.Id, StringComparer.OrdinalIgnoreCase);

        return channels
            .GroupBy(channel => string.IsNullOrWhiteSpace(channel.LogicalCallKey) ? channel.Id : channel.LogicalCallKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(channel => GetStateRank(channel.State))
                    .ThenByDescending(channel => channel.DurationSeconds)
                    .ToList();
                var primary = ordered[0];
                var createdUtcValues = ordered
                    .Where(channel => channel.CreatedUtc.HasValue)
                    .Select(channel => channel.CreatedUtc.Value)
                    .ToList();
                var bridgeId = ordered
                    .Select(channel => channel.BridgeId)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                var bridge = !string.IsNullOrWhiteSpace(bridgeId) && bridgeLookup.TryGetValue(bridgeId, out var bridgeSnapshot)
                    ? bridgeSnapshot
                    : null;
                var partyCount = GetPartyCount(ordered, bridge);
                var direction = InferCallDirection(ordered);

                return new AsteriskCallSnapshot
                {
                    Key = group.Key,
                    PrimaryChannelId = primary.Id,
                    CallerName = ordered.Select(channel => channel.CallerName).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    CallerNumber = ordered.Select(channel => channel.CallerNumber).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    ConnectedNumber = ordered.Select(channel => channel.ConnectedNumber).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    Direction = direction,
                    State = SummarizeCallState(ordered, bridge, partyCount, direction),
                    StateDetail = SummarizeCallStateDetail(ordered, bridge),
                    Application = ordered.Select(channel => channel.Application).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    ChannelCount = group.Count(),
                    PartyCount = partyCount,
                    IsOnHold = ordered.Any(channel => channel.IsOnHold),
                    IsMuted = ordered.Any(channel => channel.IsMuted),
                    BridgeId = bridge?.Id,
                    BridgeType = bridge?.BridgeType,
                    CreatedUtc = createdUtcValues.Count > 0 ? createdUtcValues.Min() : null,
                    DurationSeconds = ordered.Max(channel => channel.DurationSeconds),
                };
            })
            .OrderByDescending(call => call.DurationSeconds)
            .ThenBy(call => call.CallerName ?? call.CallerNumber ?? call.Key)
            .ToList();
    }

    private static async Task EnrichChannelsAsync(
        HttpClient client,
        IList<AsteriskChannelSnapshot> channels,
        IList<AsteriskBridgeSnapshot> bridges,
        CancellationToken cancellationToken)
    {
        var bridgeMembership = bridges
            .SelectMany(bridge => bridge.ChannelIds.Select(channelId => new { bridge.Id, bridge.BridgeType, ChannelId = channelId }))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ChannelId))
            .GroupBy(entry => entry.ChannelId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var enrichmentTasks = channels.Select(async channel =>
        {
            if (string.IsNullOrWhiteSpace(channel.Id))
            {
                return;
            }

            var holdTask = ReadBooleanVariableAsync(client, channel.Id, HoldStateVariableName, cancellationToken);
            var muteTask = ReadBooleanVariableAsync(client, channel.Id, MuteStateVariableName, cancellationToken);

            await Task.WhenAll(holdTask, muteTask);

            channel.IsOnHold = await holdTask;
            channel.IsMuted = await muteTask;

            if (bridgeMembership.TryGetValue(channel.Id, out var membership))
            {
                channel.BridgeId = membership.Id;
                channel.BridgeType = membership.BridgeType;
            }
        });

        await Task.WhenAll(enrichmentTasks);
    }

    private static int GetPartyCount(
        List<AsteriskChannelSnapshot> channels,
        AsteriskBridgeSnapshot bridge)
    {
        var participantCount = channels
            .SelectMany(channel => new[] { channel.CallerNumber, channel.ConnectedNumber })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var bridgePartyCount = bridge?.ChannelCount ?? 0;
        var channelCount = channels.Count;
        var partyCount = Math.Max(Math.Max(participantCount, bridgePartyCount), channelCount > 0 ? 1 : 0);

        return partyCount;
    }

    private static async Task<bool> ReadBooleanVariableAsync(
        HttpClient client,
        string channelId,
        string variableName,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"channels/{Uri.EscapeDataString(channelId)}/variable?variable={Uri.EscapeDataString(variableName)}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        using var document = JsonDocument.Parse(content);
        var value = GetString(document.RootElement, "value");

        return string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetStateRank(string state)
    {
        if (string.Equals(state, "Up", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (IsRingingState(state))
        {
            return 3;
        }

        if (string.Equals(state, "Dialing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Ring", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    private static string SummarizeCallState(
        IList<AsteriskChannelSnapshot> channels,
        AsteriskBridgeSnapshot bridge,
        int partyCount,
        string direction)
    {
        if (channels.Any(channel => channel.IsOnHold))
        {
            return "On hold";
        }

        var hasBridge = bridge?.ChannelCount > 1 ||
            channels.Any(channel => !string.IsNullOrWhiteSpace(channel.BridgeId));
        var hasConnectedLeg = channels.Any(channel => string.Equals(channel.State, "Up", StringComparison.OrdinalIgnoreCase));
        var hasRingingLeg = channels.Any(channel => IsRingingState(channel.State));

        if (string.Equals(direction, "Inbound", StringComparison.OrdinalIgnoreCase) &&
            !hasBridge &&
            partyCount <= 1 &&
            hasConnectedLeg)
        {
            return "Offered";
        }

        if (!hasBridge && hasRingingLeg)
        {
            return "Offering";
        }

        if (hasRingingLeg)
        {
            return "Ringing";
        }

        if (hasConnectedLeg)
        {
            return partyCount >= 3 ? "In conference" : "Connected";
        }

        return channels
            .Select(channel => channel.State)
            .FirstOrDefault(state => !string.IsNullOrWhiteSpace(state))
            ?? "Unknown";
    }

    private static string SummarizeCallStateDetail(
        IList<AsteriskChannelSnapshot> channels,
        AsteriskBridgeSnapshot bridge)
    {
        var states = channels
            .Select(channel => channel.State)
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (bridge?.ChannelCount > 1)
        {
            return $"Bridge legs: {bridge.ChannelCount} • raw states: {string.Join(", ", states)}";
        }

        return states.Count > 0
            ? $"Raw channel states: {string.Join(", ", states)}"
            : null;
    }

    private static string GetLogicalCallKey(string channelName, string channelId)
    {
        if (!string.IsNullOrWhiteSpace(channelName) &&
            (channelName.EndsWith(";1", StringComparison.OrdinalIgnoreCase) ||
                channelName.EndsWith(";2", StringComparison.OrdinalIgnoreCase)))
        {
            return channelName[..^2];
        }

        return !string.IsNullOrWhiteSpace(channelName) ? channelName : channelId;
    }

    private static string InferLegDirection(string channelName, string callerNumber, string connectedNumber)
    {
        if (!string.IsNullOrWhiteSpace(channelName))
        {
            if (channelName.EndsWith(";1", StringComparison.OrdinalIgnoreCase))
            {
                return "Outbound leg";
            }

            if (channelName.EndsWith(";2", StringComparison.OrdinalIgnoreCase))
            {
                return "Inbound leg";
            }
        }

        if (!string.IsNullOrWhiteSpace(callerNumber) && string.IsNullOrWhiteSpace(connectedNumber))
        {
            return "Inbound";
        }

        if (string.IsNullOrWhiteSpace(callerNumber) && !string.IsNullOrWhiteSpace(connectedNumber))
        {
            return "Outbound";
        }

        return "Unknown";
    }

    private static string InferCallDirection(IList<AsteriskChannelSnapshot> channels)
    {
        var distinctDirections = channels
            .Select(channel => channel.Direction)
            .Where(direction => !string.IsNullOrWhiteSpace(direction) && !string.Equals(direction, "Unknown", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctDirections.Count == 1)
        {
            return distinctDirections[0];
        }

        if (distinctDirections.Count > 1)
        {
            return "Mixed / local";
        }

        return "Unknown";
    }

    private static string GetString(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : current.ToString();
    }

    private static string GetNestedString(JsonElement element, string propertyName, string nestedPropertyName)
    {
        if (!element.TryGetProperty(propertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(nested, nestedPropertyName);
    }

    private static string FormatJson(string content, string fallback)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return fallback;
        }

        using var document = JsonDocument.Parse(content);

        return JsonSerializer.Serialize(document.RootElement, JsonSerializerOptions);
    }

    /// <summary>
    /// Disconnects an active Asterisk channel.
    /// </summary>
    /// <param name="channelId">The channel identifier to disconnect.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DisconnectChannelAsync(string channelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);

        if (!AsteriskAriConnectionUtilities.IsConfigured(_options))
        {
            throw new InvalidOperationException("Asterisk diagnostics are not configured.");
        }

        var client = _httpClientFactory.CreateClient(nameof(AsteriskDiagnosticsService));
        AsteriskAriConnectionUtilities.ApplyBasicAuthentication(client, _options);

        using var response = await client.DeleteAsync($"channels/{Uri.EscapeDataString(channelId)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        await RefreshAsync(cancellationToken);
    }
}
