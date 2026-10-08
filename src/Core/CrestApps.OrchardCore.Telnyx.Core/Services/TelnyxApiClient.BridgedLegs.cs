using System.Text.Json;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The commands that keep two bridged legs apart when one of them is moved somewhere else: the correlation each leg
/// carries in its client state, read back and rewritten, and the conference commands that set it as they move a leg.
/// </summary>
public sealed partial class TelnyxApiClient
{
    /// <summary>
    /// Reads a leg's status and the client state it carries.
    /// </summary>
    /// <param name="callControlId">The leg to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxCallStatus> GetCallStatusAsync(string callControlId, CancellationToken cancellationToken = default)
    {
        var (result, json) = await GetCallAsync(callControlId, cancellationToken);

        if (!result.Succeeded ||
            json is not JsonElement root ||
            root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object)
        {
            return new TelnyxCallStatus { Succeeded = result.Succeeded, StatusCode = result.StatusCode, ErrorBody = result.ErrorBody };
        }

        return new TelnyxCallStatus
        {
            Succeeded = true,
            StatusCode = result.StatusCode,
            IsAlive = data.TryGetProperty("is_alive", out var alive) && alive.ValueKind == JsonValueKind.True,
            ClientState = data.TryGetProperty("client_state", out var state) && state.ValueKind == JsonValueKind.String
                ? state.GetString()
                : null,
        };
    }

    /// <summary>
    /// Replaces the client state a leg carries on every later event (<c>PUT /calls/{id}/actions/client_state_update</c>).
    /// Setting the same state twice is harmless, so this is retried.
    /// </summary>
    /// <param name="callControlId">The leg.</param>
    /// <param name="clientStateJson">The new state, unencoded.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxApiResult> UpdateClientStateAsync(string callControlId, string clientStateJson, CancellationToken cancellationToken = default)
    {
        var (result, _) = await SendAsync(
            HttpMethod.Put,
            $"calls/{Uri.EscapeDataString(callControlId ?? string.Empty)}/actions/client_state_update",
            BuildClientStateBody(clientStateJson),
            retryable: true,
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Hangs a leg up, stamping the client state its hang-up event carries. Retried, like any hang-up.
    /// </summary>
    /// <param name="callControlId">The leg.</param>
    /// <param name="clientStateJson">The state its hang-up is reported with, unencoded.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> HangupWithStateAsync(string callControlId, string clientStateJson, CancellationToken cancellationToken = default)
        => PostActionAsync(callControlId, "hangup", BuildClientStateBody(clientStateJson), retryable: true, cancellationToken);

    /// <summary>
    /// Plays DTMF from a leg to the party at its other end (<c>send_dtmf</c>).
    /// </summary>
    /// <param name="callControlId">The leg.</param>
    /// <param name="digits">The digits.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> SendDtmfAsync(string callControlId, string digits, CancellationToken cancellationToken = default)
        => PostActionAsync(
            callControlId,
            "send_dtmf",
            new Dictionary<string, object>(StringComparer.Ordinal) { ["digits"] = digits },
            retryable: false,
            cancellationToken);

    /// <summary>
    /// Creates a conference around a leg and sets the client state that leg carries from then on.
    /// </summary>
    /// <param name="name">The conference name.</param>
    /// <param name="callControlId">The leg the conference is created around.</param>
    /// <param name="clientStateJson">The leg's new state, unencoded.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxConferenceApiResult> CreateConferenceWithStateAsync(
        string name,
        string callControlId,
        string clientStateJson,
        CancellationToken cancellationToken = default)
    {
        var body = BuildClientStateBody(clientStateJson);
        body["name"] = name;
        body["call_control_id"] = callControlId;

        var (result, json) = await SendAsync(HttpMethod.Post, "conferences", body, retryable: false, cancellationToken);

        return new TelnyxConferenceApiResult
        {
            Succeeded = result.Succeeded,
            StatusCode = result.StatusCode,
            ErrorBody = result.ErrorBody,
            ConferenceId = ReadDataString(json, "id"),
        };
    }

    /// <summary>
    /// Joins a leg to a conference and sets the client state that leg carries from then on.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="callControlId">The leg to join.</param>
    /// <param name="endConferenceOnExit">Whether the conference ends, hanging up everyone in it, when this leg leaves.</param>
    /// <param name="clientStateJson">The leg's new state, unencoded; <see langword="null"/> to leave it as it is.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxApiResult> JoinConferenceWithStateAsync(
        string conferenceId,
        string callControlId,
        bool endConferenceOnExit,
        string clientStateJson,
        CancellationToken cancellationToken = default)
    {
        var body = BuildClientStateBody(clientStateJson);
        body["call_control_id"] = callControlId;

        if (endConferenceOnExit)
        {
            body["end_conference_on_exit"] = true;
        }

        var (result, _) = await SendAsync(
            HttpMethod.Post,
            $"conferences/{Uri.EscapeDataString(conferenceId ?? string.Empty)}/actions/join",
            body,
            retryable: false,
            cancellationToken);

        return result;
    }
}

/// <summary>
/// A leg's status as the provider reports it.
/// </summary>
public sealed class TelnyxCallStatus : TelnyxApiResult
{
    /// <summary>
    /// Gets a value indicating whether the leg is still up.
    /// </summary>
    public bool IsAlive { get; init; }

    /// <summary>
    /// Gets the client state the leg carries, base64-encoded as Telnyx returns it.
    /// </summary>
    public string ClientState { get; init; }
}
