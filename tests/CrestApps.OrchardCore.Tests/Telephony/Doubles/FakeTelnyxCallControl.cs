using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A small stateful stand-in for the Telnyx Call Control and Conference API: legs with the client state they carry,
/// conferences with their participants, and the bridges made between legs. Every request is recorded in order, so a
/// test can assert both the exact command sequence and what it left behind.
/// </summary>
internal sealed class FakeTelnyxCallControl : HttpMessageHandler
{
    private int _conferenceCount;
    private int _legCount;

    /// <summary>Gets every request, in order.</summary>
    public List<FakeTelnyxRequest> Requests { get; } = [];

    /// <summary>Gets the client state each leg carries, decoded, by call control id.</summary>
    public Dictionary<string, string> LegStates { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the legs that have been hung up.</summary>
    public HashSet<string> HungUp { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the conferences, by id.</summary>
    public Dictionary<string, FakeTelnyxConference> Conferences { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the bridges made, as (issued on, other leg, park_after_unbridge).</summary>
    public List<(string Leg, string Other, string ParkAfterUnbridge)> Bridges { get; } = [];

    /// <summary>Gets or sets the id the next originated leg is given.</summary>
    public string NextLegId { get; set; }

    /// <summary>Gets the legs whose conference join Telnyx refuses.</summary>
    public HashSet<string> RefuseJoinFor { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the legs that have not been answered yet: a role switch or a bridge on one is refused.</summary>
    public HashSet<string> Unanswered { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the legs a bridge issued on is refused.</summary>
    public HashSet<string> RefuseBridgeFor { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets a value indicating whether a conference create is refused.</summary>
    public bool RefuseCreate { get; set; }

    /// <summary>Gets or sets a value indicating whether an originate is refused.</summary>
    public bool RefuseOriginate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a participant update is answered 200 but not applied, as a change of a
    /// supervisor's role was live: the participant list still shows what they joined with.
    /// </summary>
    public bool IgnoreParticipantUpdates { get; set; }

    /// <summary>Gives a leg a client state, as if the platform had set it earlier.</summary>
    public FakeTelnyxCallControl WithLeg(string callControlId, TelnyxOutboundBridgeState state)
    {
        LegStates[callControlId] = state?.ToClientStateJson();

        return this;
    }

    /// <summary>Starts a conference with participants, as if a supervisor had already moved the call.</summary>
    public FakeTelnyxConference WithConference(string name, params string[] participants)
    {
        var conference = new FakeTelnyxConference($"conf-{++_conferenceCount}", name);
        conference.Participants.AddRange(participants);
        Conferences[conference.Id] = conference;

        return conference;
    }

    /// <summary>The recorded commands, as "METHOD path" without the /v2 prefix or the query.</summary>
    public IReadOnlyList<string> Commands
        => Requests.Select(request => $"{request.Method} {request.Path}").ToList();

    /// <summary>The body of the only request to <paramref name="path"/>.</summary>
    public JsonElement BodyOf(string method, string path)
        => Requests.Single(request => request.Method == method && request.Path == path).Body;

    /// <summary>The state a leg's command set, decoded from its body.</summary>
    public static TelnyxOutboundBridgeState StateIn(JsonElement body)
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(body.GetProperty("client_state").GetString()));
        Assert.True(TelnyxOutboundBridgeState.TryParse(decoded, out var state));

        return state;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? default : JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
        var path = request.RequestUri.AbsolutePath;
        path = path.StartsWith("/v2/", StringComparison.Ordinal) ? path[4..] : path.TrimStart('/');
        var query = Uri.UnescapeDataString(request.RequestUri.Query ?? string.Empty);

        Requests.Add(new FakeTelnyxRequest(request.Method.Method, path, query, body));

        var segments = path.Split('/');

        if (request.Method == HttpMethod.Post && path == "calls")
        {
            if (RefuseOriginate)
            {
                return Json(HttpStatusCode.UnprocessableEntity, new { errors = new[] { new { code = "10015" } } });
            }

            var legId = NextLegId ?? $"leg-{++_legCount}";
            NextLegId = null;
            LegStates[legId] = DecodeState(body);

            return Json(HttpStatusCode.OK, new { data = new { call_control_id = legId } });
        }

        if (request.Method == HttpMethod.Get && segments is ["calls", var getLeg])
        {
            var data = new Dictionary<string, object>
            {
                ["call_control_id"] = getLeg,
                ["is_alive"] = !HungUp.Contains(getLeg),
            };

            if (LegStates.TryGetValue(getLeg, out var legState) && legState is not null)
            {
                data["client_state"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(legState));
            }

            return Json(HttpStatusCode.OK, new { data });
        }

        if (segments is ["calls", var leg, "actions", var action])
        {
            if (HungUp.Contains(leg))
            {
                return Json(HttpStatusCode.UnprocessableEntity, new { errors = new[] { new { code = "90018" } } });
            }

            if (Unanswered.Contains(leg) && action is "switch_supervisor_role" or "bridge")
            {
                return Json(HttpStatusCode.UnprocessableEntity, new { errors = new[] { new { code = "90034", title = "Call not answered yet" } } });
            }

            if (action == "bridge" && RefuseBridgeFor.Contains(leg))
            {
                return Json(HttpStatusCode.UnprocessableEntity, new { errors = new[] { new { code = "90000" } } });
            }

            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("client_state", out _))
            {
                LegStates[leg] = DecodeState(body);
            }

            switch (action)
            {
                case "hangup":
                    HungUp.Add(leg);

                    foreach (var conference in Conferences.Values)
                    {
                        conference.Participants.Remove(leg);
                    }

                    break;
                case "bridge":
                    Bridges.Add((
                        leg,
                        body.GetProperty("call_control_id").GetString(),
                        body.TryGetProperty("park_after_unbridge", out var park) ? park.GetString() : null));
                    break;
            }

            return Json(HttpStatusCode.OK, new { data = new { result = "ok" } });
        }

        if (request.Method == HttpMethod.Get && path == "conferences")
        {
            var name = ReadFilter(query, "filter[name]");
            var liveOnly = ReadFilter(query, "filter[status]") == "in_progress";
            var matches = Conferences.Values
                .Where(conference => conference.Name == name && (!liveOnly || conference.IsLive))
                .Select(conference => new { id = conference.Id, name = conference.Name, status = conference.IsLive ? "in_progress" : "completed" })
                .ToArray();

            return Json(HttpStatusCode.OK, new { data = matches });
        }

        if (request.Method == HttpMethod.Post && path == "conferences")
        {
            if (RefuseCreate)
            {
                return Json(HttpStatusCode.UnprocessableEntity, new { errors = new[] { new { code = "90044" } } });
            }

            var creator = body.GetProperty("call_control_id").GetString();

            foreach (var other in Conferences.Values)
            {
                other.Participants.Remove(creator);
            }

            var conference = WithConference(body.GetProperty("name").GetString(), creator);

            return Json(HttpStatusCode.OK, new { data = new { id = conference.Id, name = conference.Name } });
        }

        if (request.Method == HttpMethod.Get && segments is ["conferences", var listed, "participants"])
        {
            var participants = Conferences.TryGetValue(listed, out var conference)
                ? conference.Participants.Select(id => new
                {
                    call_control_id = id,
                    status = "joined",
                    muted = conference.Muted.Contains(id),
                    on_hold = false,
                    whisper_call_control_ids = conference.Whispers.TryGetValue(id, out var hearers) ? hearers : [],
                }).ToArray()
                : [];

            return Json(HttpStatusCode.OK, new { data = participants });
        }

        if (request.Method == HttpMethod.Get && segments is ["conferences", var readId] && Conferences.TryGetValue(readId, out var read))
        {
            return Json(HttpStatusCode.OK, new { data = new { id = read.Id, name = read.Name, status = read.IsLive ? "in_progress" : "completed" } });
        }

        if (segments is ["conferences", var conferenceId, "actions", var conferenceAction] &&
            Conferences.TryGetValue(conferenceId, out var target))
        {
            var participant = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("call_control_id", out var id)
                ? id.GetString()
                : null;

            switch (conferenceAction)
            {
                case "join":
                    if (RefuseJoinFor.Contains(participant))
                    {
                        return Json(HttpStatusCode.UnprocessableEntity, new { errors = new[] { new { code = "90000" } } });
                    }

                    if (!target.Participants.Contains(participant))
                    {
                        target.Participants.Add(participant);
                    }

                    if (body.TryGetProperty("client_state", out _))
                    {
                        LegStates[participant] = DecodeState(body);
                    }

                    target.RecordRole(participant, body);

                    if (body.TryGetProperty("mute", out var joinMuted) && joinMuted.GetBoolean())
                    {
                        target.Muted.Add(participant);
                    }

                    break;
                case "leave":
                    target.Participants.Remove(participant);
                    target.Whispers.Remove(participant);
                    target.Muted.Remove(participant);
                    break;
                case "update":
                    if (!target.Participants.Contains(participant))
                    {
                        return Json(HttpStatusCode.UnprocessableEntity, new { errors = new[] { new { code = "90000" } } });
                    }

                    if (!IgnoreParticipantUpdates)
                    {
                        target.RecordRole(participant, body);
                    }

                    break;
                case "mute":
                case "unmute":
                    foreach (var muted in body.GetProperty("call_control_ids").EnumerateArray().Select(item => item.GetString()))
                    {
                        if (conferenceAction == "mute")
                        {
                            target.Muted.Add(muted);
                        }
                        else
                        {
                            target.Muted.Remove(muted);
                        }
                    }

                    break;
                case "end":
                    target.Participants.Clear();
                    break;
            }

            return Json(HttpStatusCode.OK, new { data = new { result = "ok" } });
        }

        return Json(HttpStatusCode.NotFound, new { errors = new[] { new { code = "404" } } });
    }

    private static string DecodeState(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object ||
            !body.TryGetProperty("client_state", out var encoded) ||
            encoded.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded.GetString()));
    }

    private static string ReadFilter(string query, string key)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');

            if (separator > 0 && part[..separator] == key)
            {
                return part[(separator + 1)..];
            }
        }

        return null;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object payload)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
}

/// <summary>
/// One recorded request.
/// </summary>
internal sealed record FakeTelnyxRequest(string Method, string Path, string Query, JsonElement Body);

/// <summary>
/// A conference the fake keeps.
/// </summary>
internal sealed class FakeTelnyxConference
{
    public FakeTelnyxConference(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    public string Name { get; }

    public List<string> Participants { get; } = [];

    /// <summary>Gets the legs each whispering supervisor is heard by.</summary>
    public Dictionary<string, string[]> Whispers { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the participants Telnyx has muted.</summary>
    public HashSet<string> Muted { get; } = new(StringComparer.Ordinal);

    public bool IsLive => Participants.Count > 0;

    /// <summary>Keeps the whisper list a join or an update gave a participant.</summary>
    public void RecordRole(string participant, JsonElement body)
    {
        if (body.TryGetProperty("supervisor_role", out var role) &&
            role.GetString() == "whisper" &&
            body.TryGetProperty("whisper_call_control_ids", out var hearers))
        {
            Whispers[participant] = hearers.EnumerateArray().Select(item => item.GetString()).ToArray();
        }
        else if (body.TryGetProperty("supervisor_role", out _))
        {
            Whispers.Remove(participant);
        }
    }
}
