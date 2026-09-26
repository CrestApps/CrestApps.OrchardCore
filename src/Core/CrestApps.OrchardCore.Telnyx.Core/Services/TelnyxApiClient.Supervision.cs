using System.Text.Json;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The conference commands a supervisor engagement is built from: a conference made around the customer's leg with
/// no join or leave tones, the agent and the supervisor joining it quietly, the supervisor's role changed while they
/// stay on, and the live conference and its participants read back by name.
/// </summary>
/// <remarks>
/// See <see href="https://developers.telnyx.com/api-reference/conference-commands/create-conference"/>,
/// <see href="https://developers.telnyx.com/api-reference/conference-commands/join-a-conference"/>,
/// <see href="https://developers.telnyx.com/api-reference/conference-commands/update-conference-participant"/>,
/// <see href="https://developers.telnyx.com/api-reference/conference-commands/list-conferences"/> and
/// <see href="https://developers.telnyx.com/api-reference/conference-commands/list-conference-participants"/>.
/// </remarks>
public sealed partial class TelnyxApiClient
{
    // No tone when anybody joins or leaves: the customer must not hear a supervisor arrive.
    private const string SilentBeep = "never";

    /// <summary>
    /// Creates a conference around a leg, with no join or leave tones.
    /// </summary>
    /// <param name="name">The conference name.</param>
    /// <param name="callControlId">The leg the conference is created around.</param>
    /// <param name="commandId">The idempotency key Telnyx collapses a repeated command with.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxConferenceApiResult> CreateSilentConferenceAsync(
        string name,
        string callControlId,
        string commandId,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["call_control_id"] = callControlId,
            ["beep_enabled"] = SilentBeep,
        };

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            body["command_id"] = commandId;
        }

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
    /// Joins a leg to a conference with no join or leave tone, optionally as a supervisor.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="callControlId">The leg to join.</param>
    /// <param name="supervisorRole">The Telnyx supervisor role (a supervisor always joins as <c>whisper</c>, naming who hears them), or <see langword="null"/> for an ordinary participant.</param>
    /// <param name="whisperCallControlIds">The legs a whispering supervisor is heard by.</param>
    /// <param name="commandId">The idempotency key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>
    /// Nobody is ever joined muted: live, a conference a supervisor joined muted (or as Telnyx's <c>monitor</c>) stopped
    /// carrying anybody's audio to anybody.
    /// </remarks>
    public async Task<TelnyxApiResult> JoinConferenceSilentlyAsync(
        string conferenceId,
        string callControlId,
        string supervisorRole = null,
        IReadOnlyCollection<string> whisperCallControlIds = null,
        string commandId = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["call_control_id"] = callControlId,
            ["beep_enabled"] = SilentBeep,
        };

        AddSupervisorRole(body, supervisorRole, whisperCallControlIds);

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            body["command_id"] = commandId;
        }

        var (result, _) = await SendAsync(
            HttpMethod.Post,
            $"conferences/{Uri.EscapeDataString(conferenceId ?? string.Empty)}/actions/join",
            body,
            retryable: false,
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Changes a participant's supervisor role while it stays in the conference.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="callControlId">The supervisor's leg.</param>
    /// <param name="supervisorRole">The new role.</param>
    /// <param name="whisperCallControlIds">The legs a whispering supervisor is heard by.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxApiResult> UpdateConferenceSupervisorRoleAsync(
        string conferenceId,
        string callControlId,
        string supervisorRole,
        IReadOnlyCollection<string> whisperCallControlIds = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["call_control_id"] = callControlId,
        };

        AddSupervisorRole(body, supervisorRole ?? "none", whisperCallControlIds);

        var (result, _) = await SendAsync(
            HttpMethod.Post,
            $"conferences/{Uri.EscapeDataString(conferenceId ?? string.Empty)}/actions/update",
            body,
            retryable: true,
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Finds a conference of the given name that is still running. An ended conference of the same name is not returned.
    /// </summary>
    /// <param name="name">The conference name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxConferenceApiResult> FindLiveConferenceByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var (result, json) = await SendAsync(
            HttpMethod.Get,
            $"conferences?filter[name]={Uri.EscapeDataString(name ?? string.Empty)}&filter[status]=in_progress",
            body: null,
            retryable: true,
            cancellationToken);

        return new TelnyxConferenceApiResult
        {
            Succeeded = result.Succeeded,
            StatusCode = result.StatusCode,
            ErrorBody = result.ErrorBody,
            ConferenceId = ReadFirstArrayItemString(json, "id"),
        };
    }

    /// <summary>
    /// Lists the legs still joined to a conference.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The call control ids of the participants whose status is not <c>left</c>, or <see langword="null"/> when the list could not be read.</returns>
    public async Task<IReadOnlyList<string>> ListJoinedConferenceParticipantsAsync(string conferenceId, CancellationToken cancellationToken = default)
        => (await ReadJoinedConferenceParticipantsAsync(conferenceId, cancellationToken))?.Select(participant => participant.CallControlId).ToList();

    /// <summary>
    /// Lists the participants still joined to a conference, as Telnyx holds them: muted, on hold, and who each whispering
    /// supervisor is heard by.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The participants whose status is not <c>left</c>, or <see langword="null"/> when the list could not be read.</returns>
    public async Task<IReadOnlyList<TelnyxConferenceParticipant>> ReadJoinedConferenceParticipantsAsync(string conferenceId, CancellationToken cancellationToken = default)
    {
        var (result, json) = await SendAsync(
            HttpMethod.Get,
            $"conferences/{Uri.EscapeDataString(conferenceId ?? string.Empty)}/participants?page[size]=250",
            body: null,
            retryable: true,
            cancellationToken);

        if (!result.Succeeded ||
            json is not JsonElement root ||
            !root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var participants = new List<TelnyxConferenceParticipant>();

        foreach (var item in data.EnumerateArray())
        {
            if (TryReadConferenceParticipant(item, out var participant) &&
                !string.Equals(participant.Status, "left", StringComparison.OrdinalIgnoreCase))
            {
                participants.Add(participant);
            }
        }

        return participants;
    }

    // One entry of a conference's participants list. A whisper list Telnyx did not send is null, not empty: it says
    // nothing about who hears the participant.
    private static bool TryReadConferenceParticipant(JsonElement item, out TelnyxConferenceParticipant participant)
    {
        participant = null;

        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("call_control_id", out var id) ||
            id.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        List<string> hearers = null;

        if (item.TryGetProperty("whisper_call_control_ids", out var whisper) && whisper.ValueKind == JsonValueKind.Array)
        {
            hearers = [];

            foreach (var hearer in whisper.EnumerateArray())
            {
                if (hearer.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(hearer.GetString()))
                {
                    hearers.Add(hearer.GetString());
                }
            }
        }

        participant = new TelnyxConferenceParticipant(
            id.GetString(),
            item.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String ? status.GetString() : null,
            ReadTrue(item, "muted"),
            ReadTrue(item, "on_hold"),
            hearers);

        return true;
    }

    /// <summary>
    /// Reads a conference's name by its id.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The name, or <see langword="null"/> when the conference could not be read.</returns>
    public async Task<string> GetConferenceNameAsync(string conferenceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conferenceId))
        {
            return null;
        }

        var (result, json) = await SendAsync(
            HttpMethod.Get,
            $"conferences/{Uri.EscapeDataString(conferenceId)}",
            body: null,
            retryable: true,
            cancellationToken);

        return result.Succeeded ? ReadDataString(json, "name") : null;
    }

    private static bool ReadTrue(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static void AddSupervisorRole(
        Dictionary<string, object> body,
        string supervisorRole,
        IReadOnlyCollection<string> whisperCallControlIds)
    {
        if (string.IsNullOrWhiteSpace(supervisorRole))
        {
            return;
        }

        body["supervisor_role"] = supervisorRole;

        if (whisperCallControlIds is { Count: > 0 })
        {
            body["whisper_call_control_ids"] = whisperCallControlIds.ToArray();
        }
    }
}
