using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

public sealed class XapiDocumentsResourceTests
{
    private const string Agent = """{ "objectType": "Agent", "mbox": "mailto:learner@example.com" }""";

    [Fact]
    public async Task State_IsStoredReadListedAndDeleted_PerRegistration()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();

        var put = await lrs.DocumentAsync("PUT", "activities/state", State("bookmark"), new JsonObject { ["page"] = 3 });
        var other = await lrs.DocumentAsync("PUT", "activities/state", State("bookmark", XapiSamples.Registration), new JsonObject { ["page"] = 9 });
        lrs.Clock.UtcNow = lrs.Clock.UtcNow.AddMinutes(5);
        await lrs.DocumentAsync("PUT", "activities/state", State("progress"), "plain text", contentType: "text/plain");

        var read = await lrs.DocumentAsync("GET", "activities/state", State("bookmark"));
        var inRegistration = await lrs.DocumentAsync("GET", "activities/state", State("bookmark", XapiSamples.Registration));
        var list = await lrs.DocumentAsync("GET", "activities/state", State(null));
        var since = await lrs.DocumentAsync("GET", "activities/state", Add(State(null), "since", "2026-11-02T12:01:00Z"));
        var text = await lrs.DocumentAsync("GET", "activities/state", State("progress"));

        Assert.Equal(204, put.Status);
        Assert.Equal(204, other.Status);
        Assert.Equal(3, JsonNode.Parse(read.Text)["page"].GetValue<int>());
        Assert.Equal(9, JsonNode.Parse(inRegistration.Text)["page"].GetValue<int>());
        Assert.Equal(["bookmark", "progress"], JsonNode.Parse(list.Text).AsArray().Select(id => id.GetValue<string>()));
        Assert.Equal(["progress"], JsonNode.Parse(since.Text).AsArray().Select(id => id.GetValue<string>()));
        Assert.Equal("plain text", text.Text);
        Assert.Equal("application/octet-stream", text.ContentType);
        Assert.StartsWith("\"", read.Headers["ETag"]);

        Assert.Equal(204, (await lrs.DocumentAsync("DELETE", "activities/state", State("bookmark"))).Status);
        Assert.Equal(404, (await lrs.DocumentAsync("GET", "activities/state", State("bookmark"))).Status);
        Assert.Equal(204, (await lrs.DocumentAsync("DELETE", "activities/state", State(null))).Status);
        Assert.Empty(JsonNode.Parse((await lrs.DocumentAsync("GET", "activities/state", State(null))).Text).AsArray());
        Assert.Equal(200, (await lrs.DocumentAsync("GET", "activities/state", State("bookmark", XapiSamples.Registration))).Status);
    }

    [Fact]
    public async Task Post_MergesJsonObjects_AndRefusesAnythingElseOnAnExistingDocument()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();

        await lrs.DocumentAsync("POST", "activities/state", State("prefs"), new JsonObject { ["a"] = 1, ["b"] = 2 });
        var merged = await lrs.DocumentAsync("POST", "activities/state", State("prefs"), new JsonObject { ["b"] = 3, ["c"] = 4 });
        var notJson = await lrs.DocumentAsync("POST", "activities/state", State("prefs"), "text", contentType: "text/plain");
        var read = JsonNode.Parse((await lrs.DocumentAsync("GET", "activities/state", State("prefs"))).Text);

        await lrs.DocumentAsync("PUT", "activities/state", State("binary"), "raw", contentType: "application/octet-stream");
        var ontoBinary = await lrs.DocumentAsync("POST", "activities/state", State("binary"), new JsonObject { ["a"] = 1 });

        Assert.Equal(204, merged.Status);
        Assert.Equal(400, notJson.Status);
        Assert.Equal(1, read["a"].GetValue<int>());
        Assert.Equal(3, read["b"].GetValue<int>());
        Assert.Equal(4, read["c"].GetValue<int>());
        Assert.Equal(400, ontoBinary.Status);
    }

    [Fact]
    public async Task Profiles_FollowTheConcurrencyRules()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var profile = new Dictionary<string, string> { ["activityId"] = XapiSamples.Activity, ["profileId"] = "settings" };

        var created = await lrs.DocumentAsync("PUT", "activities/profile", profile, new JsonObject { ["v"] = 1 });
        var etag = created.Headers["ETag"];
        var withoutHeaders = await lrs.DocumentAsync("PUT", "activities/profile", profile, new JsonObject { ["v"] = 2 });
        var wrongMatch = await lrs.DocumentAsync("PUT", "activities/profile", profile, new JsonObject { ["v"] = 2 }, headers: new Dictionary<string, string> { ["If-Match"] = "\"0000\"" });
        var noneMatchStar = await lrs.DocumentAsync("PUT", "activities/profile", profile, new JsonObject { ["v"] = 2 }, headers: new Dictionary<string, string> { ["If-None-Match"] = "*" });
        var rightMatch = await lrs.DocumentAsync("PUT", "activities/profile", profile, new JsonObject { ["v"] = 2 }, headers: new Dictionary<string, string> { ["If-Match"] = etag });
        var read = await lrs.DocumentAsync("GET", "activities/profile", profile);
        var staleDelete = await lrs.DocumentAsync("DELETE", "activities/profile", profile, headers: new Dictionary<string, string> { ["If-Match"] = etag });

        Assert.Equal(204, created.Status);
        Assert.Equal(409, withoutHeaders.Status);
        Assert.Equal(412, wrongMatch.Status);
        Assert.Equal(412, noneMatchStar.Status);
        Assert.Equal(204, rightMatch.Status);
        Assert.Equal(2, JsonNode.Parse(read.Text)["v"].GetValue<int>());
        Assert.NotEqual(etag, read.Headers["ETag"]);
        Assert.Equal(412, staleDelete.Status);
    }

    [Fact]
    public async Task AgentProfile_IsKeptPerAgent()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var mine = new Dictionary<string, string> { ["agent"] = Agent, ["profileId"] = "cmi5LearnerPreferences" };
        var theirs = new Dictionary<string, string> { ["agent"] = """{ "mbox": "mailto:other@example.com" }""", ["profileId"] = "cmi5LearnerPreferences" };

        await lrs.DocumentAsync("PUT", "agents/profile", mine, new JsonObject { ["languagePreference"] = "fr-FR" }, headers: new Dictionary<string, string> { ["If-None-Match"] = "*" });

        Assert.Equal(200, (await lrs.DocumentAsync("GET", "agents/profile", mine)).Status);
        Assert.Equal(404, (await lrs.DocumentAsync("GET", "agents/profile", theirs)).Status);
        Assert.Equal(["cmi5LearnerPreferences"], JsonNode.Parse((await lrs.DocumentAsync("GET", "agents/profile", new Dictionary<string, string> { ["agent"] = Agent })).Text).AsArray().Select(id => id.GetValue<string>()));
    }

    [Fact]
    public async Task Requests_WithMissingOrUnknownParameters_AreRefused()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();

        Assert.Equal(400, (await lrs.DocumentAsync("GET", "activities/state", new Dictionary<string, string> { ["agent"] = Agent, ["stateId"] = "s" })).Status);
        Assert.Equal(400, (await lrs.DocumentAsync("GET", "activities/state", new Dictionary<string, string> { ["activityId"] = XapiSamples.Activity, ["stateId"] = "s" })).Status);
        Assert.Equal(400, (await lrs.DocumentAsync("GET", "activities/state", Add(State("s"), "unknown", "1"))).Status);
        Assert.Equal(400, (await lrs.DocumentAsync("GET", "activities/state", Add(State("s"), "registration", "not-a-uuid"))).Status);
        Assert.Equal(400, (await lrs.DocumentAsync("PUT", "activities/state", State(null), new JsonObject())).Status);
        Assert.Equal(400, (await lrs.DocumentAsync("GET", "agents/profile", new Dictionary<string, string> { ["agent"] = """{ "name": "no identifier" }""" })).Status);
    }

    [Fact]
    public async Task Documents_NeedThePermission_OrALaunchOfTheirActivityAndActor()
    {
        await using var lrs = await XapiTestHarness.CreateAsync(services => services.Configure<XapiOptions>(options => options.MaxDocumentBytes = 16));
        var launch = new XapiCredential
        {
            AuthorityKey = "launch",
            Launch = new XapiLaunchScope
            {
                Registration = XapiSamples.Registration,
                ActivityId = XapiSamples.Activity,
                ActorKey = "mbox:mailto:learner@example.com",
                ReadOnlyStateIds = ["LMS.LaunchData"],
            },
        };

        await lrs.DocumentAsync("PUT", "activities/state", State("LMS.LaunchData", XapiSamples.Registration), new JsonObject { ["m"] = 1 });

        Assert.Equal(403, (await lrs.DocumentAsync("GET", "activities/state", State("s"), credential: XapiTestHarness.Client("writer", write: true))).Status);
        Assert.Equal(200, (await lrs.DocumentAsync("GET", "activities/state", State("LMS.LaunchData", XapiSamples.Registration), credential: launch)).Status);
        Assert.Equal(403, (await lrs.DocumentAsync("PUT", "activities/state", State("LMS.LaunchData", XapiSamples.Registration), new JsonObject(), credential: launch)).Status);
        Assert.Equal(204, (await lrs.DocumentAsync("PUT", "activities/state", State("suspend", XapiSamples.Registration), new JsonObject(), credential: launch)).Status);
        Assert.Equal(403, (await lrs.DocumentAsync("GET", "activities/state", State("suspend"), credential: launch)).Status);
        Assert.Equal(403, (await lrs.DocumentAsync("GET", "activities/state", new Dictionary<string, string> { ["activityId"] = XapiSamples.Course, ["agent"] = Agent, ["registration"] = XapiSamples.Registration, ["stateId"] = "s" }, credential: launch)).Status);
        Assert.Equal(413, (await lrs.DocumentAsync("PUT", "activities/state", State("big"), Encoding.UTF8.GetBytes(new string('x', 32)), contentType: "text/plain")).Status);
    }

    [Fact]
    public async Task Round9H1_ADocumentStoredAsHtml_IsServedAsASandboxedAttachment()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var profile = new Dictionary<string, string> { ["agent"] = Agent, ["profileId"] = "page" };

        await lrs.DocumentAsync("PUT", "agents/profile", profile, "<script>alert(document.cookie)</script>", contentType: "text/html", headers: new Dictionary<string, string> { ["If-None-Match"] = "*" });
        await lrs.DocumentAsync("PUT", "activities/state", State("json"), new JsonObject { ["a"] = 1 });

        var html = await lrs.DocumentAsync("GET", "agents/profile", profile);
        var json = await lrs.DocumentAsync("GET", "activities/state", State("json"));

        Assert.Equal(200, html.Status);
        Assert.Equal("application/octet-stream", html.ContentType);
        Assert.Equal("sandbox", html.Headers["Content-Security-Policy"]);
        Assert.StartsWith("attachment", html.Headers["Content-Disposition"]);
        Assert.Equal("<script>alert(document.cookie)</script>", html.Text);

        Assert.StartsWith("application/json", json.ContentType);
        Assert.Equal("sandbox", json.Headers["Content-Security-Policy"]);
        Assert.StartsWith("attachment", json.Headers["Content-Disposition"]);
    }

    private static Dictionary<string, string> State(string stateId, string registration = null)
    {
        var query = new Dictionary<string, string>
        {
            ["activityId"] = XapiSamples.Activity,
            ["agent"] = Agent,
        };

        if (stateId is not null)
        {
            query["stateId"] = stateId;
        }

        if (registration is not null)
        {
            query["registration"] = registration;
        }

        return query;
    }

    private static Dictionary<string, string> Add(Dictionary<string, string> query, string name, string value)
    {
        query[name] = value;

        return query;
    }
}
