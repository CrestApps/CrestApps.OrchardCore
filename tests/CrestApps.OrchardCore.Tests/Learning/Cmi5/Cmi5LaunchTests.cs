using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Cmi5;

/// <summary>
/// Launching cmi5 AUs, as cmi5 Quartz section 8 and 10 ask: a registration per enrollment and item, the launch data in
/// the state API, the launched statement, the learner preferences, the launch URL parameters with an account actor, a
/// fetch URL that hands the auth token out once, and abandoned for a session left open.
/// </summary>
public sealed class Cmi5LaunchTests
{
    [Fact]
    public async Task Launch_GivesARegistration_WritesTheLaunchDataAndTheLaunchedStatement_AndBuildsTheUrl()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");

        var launch = await h.LaunchAsync("ana");
        var parameters = Cmi5TestHarness.LaunchParameters(launch.LaunchUrl);
        var actor = JsonNode.Parse(parameters["actor"]);
        var state = await h.StateAsync(enrollment.ItemId);
        var activityId = parameters["activityId"];

        Assert.True(launch.Succeeded, launch.Error);
        Assert.False(launch.OwnWindow);
        Assert.StartsWith("/learning/packages/run/", launch.LaunchUrl);
        Assert.Contains("/quiz/index.html?lang=en&", launch.LaunchUrl);
        Assert.Equal(Cmi5TestHarness.HomePage + "/learning/xapi/", parameters["endpoint"]);
        Assert.StartsWith(Cmi5TestHarness.HomePage + "/learning/cmi5/fetch/", parameters["fetch"]);
        Assert.Equal(launch.Registration, parameters["registration"]);
        Assert.Equal(state.Cmi5.Registration, launch.Registration);
        Assert.Equal("ana", actor["account"]["name"].GetValue<string>());
        Assert.Equal(Cmi5TestHarness.HomePage, actor["account"]["homePage"].GetValue<string>());
        Assert.Null(actor["mbox"]);
        Assert.StartsWith(Cmi5TestHarness.HomePage + "/learning/cmi5/" + Cmi5TestHarness.Package + "/au/", activityId);

        var admin = Xapi.XapiTestHarness.Admin;
        var launchData = await h.LrsAsync(XapiRequest.Create("GET", "activities/state", query: new Dictionary<string, string>
        {
            ["activityId"] = activityId,
            ["agent"] = parameters["actor"],
            ["registration"] = launch.Registration,
            ["stateId"] = "LMS.LaunchData",
        }), admin);
        var data = JsonNode.Parse(launchData.Text);

        Assert.Equal(200, launchData.Status);
        Assert.Equal("Normal", data["launchMode"].GetValue<string>());
        Assert.Equal("Passed", data["moveOn"].GetValue<string>());
        Assert.Equal(0.8m, data["masteryScore"].GetValue<decimal>());
        Assert.Equal("level=2", data["launchParameters"].GetValue<string>());
        Assert.Equal(Cmi5TestHarness.HomePage + "/learning/cmi5/return", data["returnURL"].GetValue<string>());
        Assert.Equal(Cmi5TestHarness.FirstAu, data["contextTemplate"]["contextActivities"]["grouping"][0]["id"].GetValue<string>());
        Assert.Equal(launch.SessionId, data["contextTemplate"]["extensions"][Cmi5Vocabulary.Extensions.SessionId].GetValue<string>());

        var preferences = await h.LrsAsync(XapiRequest.Create("GET", "agents/profile", query: new Dictionary<string, string> { ["agent"] = parameters["actor"], ["profileId"] = "cmi5LearnerPreferences" }), admin);

        Assert.Equal("fr-FR", JsonNode.Parse(preferences.Text)["languagePreference"].GetValue<string>());

        var statements = JsonNode.Parse((await h.LrsAsync(XapiRequest.Create("GET", "statements", query: new Dictionary<string, string> { ["registration"] = launch.Registration }), admin)).Text)["statements"].AsArray();
        var launched = Assert.Single(statements);

        Assert.Equal(Cmi5Vocabulary.Verbs.Launched, launched["verb"]["id"].GetValue<string>());
        Assert.Equal(activityId, launched["object"]["id"].GetValue<string>());
        Assert.Equal(Cmi5Vocabulary.Categories.Cmi5, launched["context"]["contextActivities"]["category"][0]["id"].GetValue<string>());
        Assert.Equal("Normal", launched["context"]["extensions"][Cmi5Vocabulary.Extensions.LaunchMode].GetValue<string>());
        Assert.Equal("Passed", launched["context"]["extensions"][Cmi5Vocabulary.Extensions.MoveOn].GetValue<string>());
        Assert.DoesNotContain("fetch", launched.ToJsonString());
    }

    [Fact]
    public async Task AbsoluteAu_OpensInAWindowOfItsOwn_AtItsHttpsUrl()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        await h.EnrollAsync("ana");

        var launch = await h.LaunchAsync("ana", au: 1);

        Assert.True(launch.OwnWindow);
        Assert.StartsWith("https://media.example.com/video.html?endpoint=", launch.LaunchUrl);
    }

    [Fact]
    public async Task Fetch_HandsTheTokenOutOnce_ForThatActorRegistrationAndAu_UntilItExpires()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        var first = await h.FetchAsync(launch.LaunchUrl);
        var second = await h.FetchAsync(launch.LaunchUrl);
        var credential = await h.CredentialAsync(first);
        var parameters = Cmi5TestHarness.LaunchParameters(launch.LaunchUrl);

        Assert.NotNull(first.AuthToken);
        Assert.Null(second.AuthToken);
        Assert.Equal("1", second.ErrorCode);
        Assert.Equal(launch.Registration, credential.Launch.Registration);
        Assert.Equal(parameters["activityId"], credential.Launch.ActivityId);
        Assert.Equal("account:" + Cmi5TestHarness.HomePage + "|ana", credential.Launch.ActorKey);
        Assert.Contains("LMS.LaunchData", credential.Launch.ReadOnlyStateIds);
        Assert.Equal("3", (await h.RunAsync(services => services.GetRequiredService<Cmi5LaunchService>().FetchAsync("not-a-token"))).ErrorCode);

        var later = await h.LaunchAsync("ana");

        h.Clock.UtcNow = h.Clock.UtcNow.Add(Cmi5LaunchService.SessionLifetime).AddMinutes(1);

        Assert.Equal("2", (await h.FetchAsync(later.LaunchUrl)).ErrorCode);
        Assert.Null(await h.CredentialAsync(first));
    }

    [Fact]
    public async Task ASecondLaunch_KeepsTheRegistration_AndAbandonsTheSessionLeftOpen()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana");

        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(10);

        var second = await h.LaunchAsync("ana");
        var state = await h.StateAsync(enrollment.ItemId);
        var statements = JsonNode.Parse((await h.LrsAsync(XapiRequest.Create("GET", "statements", query: new Dictionary<string, string> { ["registration"] = first.Registration, ["verb"] = Cmi5Vocabulary.Verbs.Abandoned }), Xapi.XapiTestHarness.Admin)).Text)["statements"].AsArray();
        var abandoned = Assert.Single(statements);

        Assert.Equal(first.Registration, second.Registration);
        Assert.Equal(first.SessionId, abandoned["context"]["extensions"][Cmi5Vocabulary.Extensions.SessionId].GetValue<string>());
        Assert.Equal("PT0H10M0.00S", abandoned["result"]["duration"].GetValue<string>());
        Assert.NotNull(state.Cmi5.Sessions.Single(session => session.SessionId == first.SessionId).AbandonedUtc);
        Assert.Equal(2, state.Sessions);
        Assert.Equal("2", (await h.FetchAsync(first.LaunchUrl)).ErrorCode);
    }

    [Fact]
    public async Task Launch_IsRefused_WithoutAnActiveEnrollment_AndReviewsACompletedOne()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();

        Assert.Equal(PackageRuntimeErrors.NotEnrolled, (await h.LaunchAsync("bob")).Error);

        var enrollment = await h.EnrollAsync("ana");

        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CrestApps.OrchardCore.Learning.Core.Services.EnrollmentStore>();
            var record = await store.FindByIdAsync(enrollment.ItemId);

            record.Status = EnrollmentStatus.Completed;
            await store.SaveAsync(record);

            return true;
        });

        var review = await h.LaunchAsync("ana");
        var data = JsonNode.Parse((await h.LrsAsync(XapiRequest.Create("GET", "activities/state", query: new Dictionary<string, string>
        {
            ["activityId"] = Cmi5TestHarness.LaunchParameters(review.LaunchUrl)["activityId"],
            ["agent"] = Cmi5TestHarness.LaunchParameters(review.LaunchUrl)["actor"],
            ["registration"] = review.Registration,
            ["stateId"] = "LMS.LaunchData",
        }), Xapi.XapiTestHarness.Admin)).Text);

        Assert.True(review.Review);
        Assert.Equal("Review", data["launchMode"].GetValue<string>());
    }
}
