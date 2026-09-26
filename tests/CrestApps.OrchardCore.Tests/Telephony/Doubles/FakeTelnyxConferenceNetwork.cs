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
/// <para>What it models, from Telnyx's documentation and from what was seen live:</para>
/// <list type="bullet">
/// <item>A call joined to a conference leaves the one it was in. <c>actions/leave</c> parks it.</item>
/// <item>A conference is <em>ended</em> when a participant joined with <c>end_conference_on_exit</c> leaves it, or by
/// <c>actions/end</c>; it <em>expires</em> when its last participant leaves ("Conferences will expire after all
/// participants have left"). Either way everyone still in it is hung up.</item>
/// <item>The call a conference was created from stays bound to it wherever it goes: when that conference is ended it is
/// hung up with cause <c>time_limit</c>, even after it left with <c>actions/leave</c> and joined another conference (seen
/// live twice: an extension call's colleague, merged into another conference, was hung up the moment the agent's leg
/// with <c>end_conference_on_exit</c> hung up). A creator that left with <c>actions/leave</c> outlives its conference
/// expiring (seen live: a parked caller left the conference it was created from, which expired, and was answered later).
/// A creator moved by a join alone is taken as hung up by its conference expiring too, since that was never seen.</item>
/// <item>A call that has not been answered cannot join or create a conference (<c>90034</c>, "Call not answered yet"),
/// and a conference name that is in use cannot be created again (<c>90033</c>).</item>
/// </list>
/// <para>
/// Every leg that ends is queued as a <c>call.hangup</c> event carrying its client state, so a test can hand it to the
/// outbound-bridge orchestrator, which releases the other half of a pair the way it does live.
/// </para>
/// </remarks>
internal sealed class FakeTelnyxConferenceNetwork : HttpMessageHandler
{
    private readonly Dictionary<string, Leg> _legs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Conference> _conferences = new(StringComparer.Ordinal);
    private readonly HashSet<string> _refusedJoins = new(StringComparer.Ordinal);
    private int _conferenceCount;

    /// <summary>
    /// The commands sent, as "METHOD path?query", in order.
    /// </summary>
    public List<string> Commands { get; } = [];

    /// <summary>
    /// The hang-ups not yet handed to the orchestrator.
    /// </summary>
    public Queue<TelnyxCallEvent> PendingEvents { get; } = new();

    /// <summary>
    /// Adds a live leg carrying <paramref name="state"/>.
    /// </summary>
    public void AddLeg(string callControlId, TelnyxOutboundBridgeState state, bool answered = true)
        => _legs[callControlId] = new Leg { Id = callControlId, ClientState = state?.ToClientState(), Answered = answered };

    /// <summary>
    /// The leg answers; returns its <c>call.answered</c> event, carrying its client state, for the orchestrator.
    /// </summary>
    public TelnyxCallEvent Answer(string legId)
    {
        var leg = _legs[legId];
        leg.Answered = true;

        return new TelnyxCallEvent { EventType = "call.answered", CallControlId = legId, ClientState = Decode(leg.ClientState) };
    }

    /// <summary>
    /// Makes a conference named <paramref name="name"/> from <paramref name="creatorLegId"/>, as an earlier attempt would.
    /// </summary>
    public string CreateConference(string name, string creatorLegId)
    {
        var conference = new Conference { Id = $"conference-{++_conferenceCount}", Name = name, CreatorId = creatorLegId };
        _conferences[conference.Id] = conference;
        Join(conference, _legs[creatorLegId], endConferenceOnExit: false);

        return conference.Id;
    }

    public void JoinConference(string conferenceId, string legId, bool endConferenceOnExit)
        => Join(_conferences[conferenceId], _legs[legId], endConferenceOnExit);

    /// <summary>
    /// Has Telnyx refuse to join <paramref name="legId"/> to any conference.
    /// </summary>
    public void RefuseJoinsOf(string legId)
        => _refusedJoins.Add(legId);

    /// <summary>
    /// A party hangs up on their own.
    /// </summary>
    public void PartyHangsUp(string legId)
        => Hangup(_legs[legId], "normal_clearing");

    public bool IsAlive(string legId)
        => _legs.TryGetValue(legId, out var leg) && leg.Alive;

    /// <summary>
    /// The live legs in the running conference named <paramref name="name"/>, in ordinal order, or none.
    /// </summary>
    public IReadOnlyList<string> MembersOf(string name)
        => _conferences.Values.FirstOrDefault(conference => conference.Active && conference.Name == name)?.Members.Order(StringComparer.Ordinal).ToList() ?? [];

    /// <summary>
    /// The state a leg carries now.
    /// </summary>
    public TelnyxOutboundBridgeState StateOf(string legId)
        => TelnyxOutboundBridgeState.TryParseEncoded(_legs[legId].ClientState, out var state) ? state : null;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
        var query = Uri.UnescapeDataString(request.RequestUri.Query);
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        using var json = string.IsNullOrWhiteSpace(body) ? null : JsonDocument.Parse(body);
        var segments = path.Trim('/').Split('/');

        Commands.Add($"{request.Method} {path.Replace("/v2/", string.Empty, StringComparison.Ordinal)}{query}");

        if (segments.Length >= 3 && segments[1] == "calls")
        {
            return Call(request, segments, json);
        }

        if (segments.Length >= 2 && segments[1] == "conferences")
        {
            return ConferenceCommand(request, segments, query, json);
        }

        return Ok();
    }

    private HttpResponseMessage Call(HttpRequestMessage request, string[] segments, JsonDocument json)
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
                break;
            case "hangup":
                Hangup(leg, "normal_clearing");
                break;
        }

        return Ok();
    }

    private HttpResponseMessage ConferenceCommand(HttpRequestMessage request, string[] segments, string query, JsonDocument json)
    {
        if (segments.Length == 2 && request.Method == HttpMethod.Get)
        {
            var name = NameFilter(query);
            var found = _conferences.Values.Where(conference => conference.Active && conference.Name == name)
                .Select(conference => new { id = conference.Id, name = conference.Name, status = "in_progress" });

            return Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new { data = found }));
        }

        if (segments.Length == 2 && request.Method == HttpMethod.Post)
        {
            var creator = _legs[Read(json, "call_control_id")];
            var name = Read(json, "name");

            if (_conferences.Values.Any(conference => conference.Active && conference.Name == name))
            {
                return Respond(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90033","title":"Conference with given name already exists"}]}""");
            }

            if (Refusal(creator) is { } refused)
            {
                return refused;
            }

            creator.ClientState = Read(json, "client_state") ?? creator.ClientState;

            return Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new { data = new { id = CreateConference(name, creator.Id) } }));
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

                if (Refusal(joining) is { } refused)
                {
                    return refused;
                }

                if (target.Members.Contains(joining.Id))
                {
                    return Respond(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90044","title":"Participant must not join the same conference twice."}]}""");
                }

                Join(target, joining, json.RootElement.TryGetProperty("end_conference_on_exit", out var endOnExit) && endOnExit.GetBoolean());
                break;
            case "leave":
                var leaving = _legs[Read(json, "call_control_id")];

                if (leaving.ConferenceId != target.Id)
                {
                    return Respond(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90045","title":"Call is not in the conference"}]}""");
                }

                if (target.CreatorId == leaving.Id)
                {
                    target.CreatorLeft = true;
                }

                Remove(leaving);
                break;
            case "end":
                End(target, forced: true);
                break;
        }

        return Ok();
    }

    private HttpResponseMessage Refusal(Leg leg)
    {
        if (!leg.Alive)
        {
            return Ended();
        }

        if (!leg.Answered)
        {
            return Respond(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90034","title":"Call not answered yet"}]}""");
        }

        return _refusedJoins.Contains(leg.Id) ? Ended() : null;
    }

    private void Join(Conference conference, Leg leg, bool endConferenceOnExit)
    {
        Remove(leg);
        conference.Members.Add(leg.Id);
        leg.ConferenceId = conference.Id;
        leg.EndConferenceOnExit = endConferenceOnExit;
    }

    private void Remove(Leg leg)
    {
        if (leg.ConferenceId is null || !_conferences.TryGetValue(leg.ConferenceId, out var conference))
        {
            return;
        }

        conference.Members.Remove(leg.Id);
        leg.ConferenceId = null;

        var endsIt = leg.EndConferenceOnExit;
        leg.EndConferenceOnExit = false;

        if (endsIt)
        {
            End(conference, forced: true);
        }
        else if (conference.Members.Count == 0)
        {
            End(conference, forced: false);
        }
    }

    private void End(Conference conference, bool forced)
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

        if ((forced || !conference.CreatorLeft) && _legs.TryGetValue(conference.CreatorId, out var creator))
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
        Remove(leg);
        PendingEvents.Enqueue(new TelnyxCallEvent
        {
            EventType = "call.hangup",
            CallControlId = leg.Id,
            HangupCause = cause,
            ClientState = Decode(leg.ClientState),
        });
    }

    private static string NameFilter(string query)
    {
        const string Prefix = "?filter[name]=";

        if (!query.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = query[Prefix.Length..];
        var next = name.IndexOf('&', StringComparison.Ordinal);

        return next < 0 ? name : name[..next];
    }

    private static string Decode(string clientState)
        => clientState is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(clientState));

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

        public bool Answered { get; set; } = true;

        public string ClientState { get; set; }

        public string ConferenceId { get; set; }

        public bool EndConferenceOnExit { get; set; }
    }

    private sealed class Conference
    {
        public string Id { get; init; }

        public string Name { get; init; }

        public string CreatorId { get; init; }

        public bool CreatorLeft { get; set; }

        public bool Active { get; set; } = true;

        public HashSet<string> Members { get; } = new(StringComparer.Ordinal);
    }
}
