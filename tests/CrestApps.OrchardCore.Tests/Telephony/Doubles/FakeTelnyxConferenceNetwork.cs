using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A small model of Telnyx's calls and conferences behind the API client, for tests that follow a merge and a leave
/// through to who is still on the line.
/// </summary>
/// <remarks>
/// <para>What it models:</para>
/// <list type="bullet">
/// <item>A call joined to a conference leaves the one it was in.</item>
/// <item>A conference ends when a participant joined with <c>end_conference_on_exit</c> leaves it, when it is ended
/// (<c>actions/end</c>), or when its last participant leaves (Telnyx: "Conferences will expire after all participants
/// have left"). Ending it hangs up everyone still in it.</item>
/// <item>The call a conference was created from stays bound to it until it leaves it with <c>actions/leave</c> ("moves it
/// back to parked state"): moved away by joining another conference, it is still hung up, with cause
/// <c>time_limit</c>, when its first conference ends. Seen live: an extension call's colleague, moved into a merge's
/// conference by a join, was hung up with <c>time_limit</c> the moment the extension's own conference ended. A caller
/// that left the conference it was created from with <c>actions/leave</c> outlived that conference ending.</item>
/// </list>
/// <para>
/// Every leg that ends is queued as a <c>call.hangup</c> event carrying its client state, so a test can hand them to the
/// outbound-bridge orchestrator, which releases the other half of a pair the way it does live.
/// </para>
/// </remarks>
internal sealed class FakeTelnyxConferenceNetwork : HttpMessageHandler
{
    private readonly Dictionary<string, Leg> _legs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Conference> _conferences = new(StringComparer.Ordinal);
    private int _conferenceCount;

    /// <summary>
    /// The commands sent, as "METHOD path", in order.
    /// </summary>
    public List<string> Commands { get; } = [];

    /// <summary>
    /// The hang-ups not yet handed to the orchestrator.
    /// </summary>
    public Queue<TelnyxCallEvent> PendingEvents { get; } = new();

    /// <summary>
    /// Adds a live leg carrying <paramref name="state"/>.
    /// </summary>
    public void AddLeg(string callControlId, TelnyxOutboundBridgeState state)
        => _legs[callControlId] = new Leg { Id = callControlId, ClientState = state?.ToClientState() };

    /// <summary>
    /// Makes a conference named <paramref name="name"/> from <paramref name="creatorLegId"/>.
    /// </summary>
    public string CreateConference(string name, string creatorLegId)
    {
        var conference = new Conference { Id = $"conference-{++_conferenceCount}", Name = name, CreatorId = creatorLegId };
        _conferences[conference.Id] = conference;
        Join(conference, _legs[creatorLegId], endConferenceOnExit: false);

        return conference.Id;
    }

    /// <summary>
    /// Joins a leg to a conference, as the orchestrator does when it connects an extension call.
    /// </summary>
    public void JoinConference(string conferenceId, string legId, bool endConferenceOnExit)
        => Join(_conferences[conferenceId], _legs[legId], endConferenceOnExit);

    /// <summary>
    /// A party hangs up on their own.
    /// </summary>
    public void PartyHangsUp(string legId)
        => Hangup(_legs[legId], "normal_clearing");

    public bool IsAlive(string legId)
        => _legs.TryGetValue(legId, out var leg) && leg.Alive;

    /// <summary>
    /// The live legs in the running conference named <paramref name="name"/>, or none when it is not running.
    /// </summary>
    public IReadOnlyList<string> MembersOf(string name)
        => _conferences.Values.FirstOrDefault(conference => conference.Active && conference.Name == name)?.Members.Order(StringComparer.Ordinal).ToList() ?? [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
        var query = Uri.UnescapeDataString(request.RequestUri.Query);
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        using var json = string.IsNullOrWhiteSpace(body) ? null : JsonDocument.Parse(body);
        var segments = path.Trim('/').Split('/');

        Commands.Add($"{request.Method} {path.Replace("/v2/", string.Empty, StringComparison.Ordinal)}{query}");

        // calls/{id}, calls/{id}/actions/{action}
        if (segments.Length >= 3 && segments[1] == "calls")
        {
            if (!_legs.TryGetValue(segments[2], out var leg))
            {
                return Respond(HttpStatusCode.NotFound, """{"errors":[{"code":"90015","title":"Call not found"}]}""");
            }

            if (segments.Length == 3 && request.Method == HttpMethod.Get)
            {
                return Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new
                {
                    data = new { call_control_id = leg.Id, is_alive = leg.Alive, client_state = leg.ClientState },
                }));
            }

            if (!leg.Alive)
            {
                return Ended();
            }

            switch (segments[^1])
            {
                case "client_state_update":
                    leg.ClientState = Read(json, "client_state");
                    return Ok();
                case "hangup":
                    Hangup(leg, "normal_clearing");
                    return Ok();
            }

            return Ok();
        }

        // conferences, conferences/{id}/actions/{action}, conferences/{id}/participants
        if (segments.Length >= 2 && segments[1] == "conferences")
        {
            if (segments.Length == 2 && request.Method == HttpMethod.Get)
            {
                var name = query.StartsWith("?filter[name]=", StringComparison.Ordinal) ? query["?filter[name]=".Length..] : null;
                var found = _conferences.Values.Where(conference => conference.Active && conference.Name == name)
                    .Select(conference => new { id = conference.Id, name = conference.Name });

                return Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new { data = found }));
            }

            if (segments.Length == 2 && request.Method == HttpMethod.Post)
            {
                var creator = _legs[Read(json, "call_control_id")];

                if (!creator.Alive)
                {
                    return Ended();
                }

                creator.ClientState = Read(json, "client_state") ?? creator.ClientState;

                return Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new { data = new { id = CreateConference(Read(json, "name"), creator.Id) } }));
            }

            if (!_conferences.TryGetValue(segments[2], out var target) || !target.Active)
            {
                return Respond(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90039","title":"Conference has already ended"}]}""");
            }

            if (segments[^1] == "participants")
            {
                return Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new
                {
                    data = target.Members.Select(member => new { call_control_id = member, status = "joined" }),
                }));
            }

            switch (segments[^1])
            {
                case "join":
                    var joining = _legs[Read(json, "call_control_id")];

                    if (!joining.Alive)
                    {
                        return Ended();
                    }

                    if (target.Members.Contains(joining.Id))
                    {
                        return Respond(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90044","title":"Participant must not join the same conference twice."}]}""");
                    }

                    Join(target, joining, json.RootElement.TryGetProperty("end_conference_on_exit", out var endOnExit) && endOnExit.GetBoolean());
                    return Ok();
                case "leave":
                    var leaving = _legs[Read(json, "call_control_id")];

                    if (leaving.ConferenceId != target.Id)
                    {
                        return Respond(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90045","title":"Call is not in the conference"}]}""");
                    }

                    Remove(leaving, releaseCreator: true);
                    return Ok();
                case "end":
                    End(target);
                    return Ok();
            }
        }

        return Ok();
    }

    private void Join(Conference conference, Leg leg, bool endConferenceOnExit)
    {
        // Joining another conference takes the leg out of the one it was in, but not off the one it created.
        Remove(leg, releaseCreator: false);
        conference.Members.Add(leg.Id);
        leg.ConferenceId = conference.Id;
        leg.EndConferenceOnExit = endConferenceOnExit;
    }

    private void Remove(Leg leg, bool releaseCreator)
    {
        if (leg.ConferenceId is null || !_conferences.TryGetValue(leg.ConferenceId, out var conference))
        {
            return;
        }

        conference.Members.Remove(leg.Id);
        leg.ConferenceId = null;

        var endsIt = leg.EndConferenceOnExit;
        leg.EndConferenceOnExit = false;

        if (releaseCreator && conference.CreatorId == leg.Id)
        {
            conference.CreatorBound = false;
        }

        if (endsIt || conference.Members.Count == 0)
        {
            End(conference);
        }
    }

    private void End(Conference conference)
    {
        if (!conference.Active)
        {
            return;
        }

        conference.Active = false;

        foreach (var member in conference.Members.ToList())
        {
            _legs[member].ConferenceId = null;
            Hangup(_legs[member], "time_limit");
        }

        conference.Members.Clear();

        if (conference.CreatorBound && _legs.TryGetValue(conference.CreatorId, out var creator))
        {
            Hangup(creator, "time_limit");
        }
    }

    private void Hangup(Leg leg, string cause)
    {
        if (!leg.Alive)
        {
            return;
        }

        leg.Alive = false;
        Remove(leg, releaseCreator: false);
        PendingEvents.Enqueue(new TelnyxCallEvent
        {
            EventType = "call.hangup",
            CallControlId = leg.Id,
            HangupCause = cause,
            ClientState = leg.ClientState is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(leg.ClientState)),
        });
    }

    private static string Read(JsonDocument json, string property)
        => json is not null && json.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static HttpResponseMessage Ok()
        => Respond(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");

    private static HttpResponseMessage Ended()
        => Respond(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90018","title":"Call has already ended"}]}""");

    private static HttpResponseMessage Respond(HttpStatusCode statusCode, string json)
        => new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class Leg
    {
        public string Id { get; init; }

        public bool Alive { get; set; } = true;

        public string ClientState { get; set; }

        public string ConferenceId { get; set; }

        public bool EndConferenceOnExit { get; set; }
    }

    private sealed class Conference
    {
        public string Id { get; init; }

        public string Name { get; init; }

        public string CreatorId { get; init; }

        public bool CreatorBound { get; set; } = true;

        public bool Active { get; set; } = true;

        public HashSet<string> Members { get; } = new(StringComparer.Ordinal);
    }
}
