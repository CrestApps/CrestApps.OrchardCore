using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Tests.Learning.Xapi;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Cmi5;

/// <summary>
/// The cmi5 statement rules in the LRS (cmi5 Quartz section 9), and what a registration counts for: satisfied for each
/// AU, block and course whose moveOn is met, and the package item's result in Learning, marked self-reported.
/// </summary>
public sealed class Cmi5StatementTests
{
    private const string Answered = "http://adlnet.gov/expapi/verbs/answered";

    [Fact]
    public async Task AFullSession_PassesTheAu_SatisfiesItAndItsBlock_AndScoresTheItem()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var au = await AuAsync(h, "ana");

        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Initialized))).Status);
        Assert.Equal(200, (await au.PostAsync(au.Allowed(Answered))).Status);
        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Passed, success: true, scaled: 0.9m, moveOn: true))).Status);
        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Completed, completion: true, moveOn: true))).Status);
        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Terminated))).Status);

        var state = await h.StateAsync(enrollment.ItemId);
        var progress = await h.ProgressAsync(enrollment.ItemId);
        var satisfied = await SatisfiedObjectsAsync(h, au.Registration);

        Assert.True(state.Cmi5.Aus.Single(item => item.AuId == Cmi5TestHarness.FirstAu).Satisfied);
        Assert.Equal(["https://example.com/courses/forklift/block/1"], state.Cmi5.SatisfiedBlocks);
        Assert.False(state.Cmi5.CourseSatisfied);
        Assert.Contains(au.ActivityId, satisfied);
        Assert.Equal(2, satisfied.Count);
        Assert.Equal(90m, progress.Score);
        Assert.True(progress.ResultSelfReported);
        Assert.NotEqual(CourseItemState.Completed, progress.State);
    }

    [Fact]
    public async Task WhenEveryAuIsSatisfied_TheCourseIsSatisfied_AndTheItemCompletes()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var quiz = await AuAsync(h, "ana");

        await quiz.PostAsync(quiz.Statement(Cmi5Vocabulary.Verbs.Initialized));
        await quiz.PostAsync(quiz.Statement(Cmi5Vocabulary.Verbs.Passed, success: true, scaled: 0.85m, moveOn: true));
        await quiz.PostAsync(quiz.Statement(Cmi5Vocabulary.Verbs.Terminated));

        var video = await AuAsync(h, "ana", au: 1);

        await video.PostAsync(video.Statement(Cmi5Vocabulary.Verbs.Initialized));
        Assert.Equal(200, (await video.PostAsync(video.Statement(Cmi5Vocabulary.Verbs.Completed, completion: true, moveOn: true))).Status);
        await video.PostAsync(video.Statement(Cmi5Vocabulary.Verbs.Terminated));

        var state = await h.StateAsync(enrollment.ItemId);
        var progress = await h.ProgressAsync(enrollment.ItemId);
        var satisfied = await SatisfiedObjectsAsync(h, quiz.Registration);

        Assert.True(state.Cmi5.CourseSatisfied);
        Assert.Contains(Cmi5TestHarness.HomePage + "/learning/cmi5/" + Cmi5TestHarness.Package + "/course", satisfied);
        Assert.Equal(4, satisfied.Count);
        Assert.Equal(CourseItemState.Completed, progress.State);
        Assert.True(progress.Passed);
        Assert.Equal(85m, progress.Score);
        Assert.True(progress.ResultSelfReported);
    }

    [Fact]
    public async Task OrderRules_AreEnforced_With403()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var au = await AuAsync(h, "ana");

        Assert.Equal(403, (await au.PostAsync(au.Allowed(Answered))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Completed, completion: true, moveOn: true))).Status);
        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Initialized))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Initialized))).Status);
        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Terminated))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Allowed(Answered))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Terminated))).Status);
    }

    [Fact]
    public async Task Cmi5DefinedStatements_FollowTheirRules_With403()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var au = await AuAsync(h, "ana");

        await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Initialized));

        var wrongObject = au.Statement(Cmi5Vocabulary.Verbs.Completed, completion: true, moveOn: true);
        var noSession = au.Statement(Cmi5Vocabulary.Verbs.Completed, completion: true, moveOn: true);

        wrongObject["object"]["id"] = "https://example.com/elsewhere";
        noSession["context"]["extensions"] = new JsonObject();

        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Satisfied))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Launched))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Answered))).Status);
        Assert.Equal(403, (await au.PostAsync(wrongObject)).Status);
        Assert.Equal(403, (await au.PostAsync(noSession)).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Completed, completion: false, moveOn: true))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Completed, completion: true, moveOn: false))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Passed, success: true, scaled: 0.5m, moveOn: true))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Passed, success: true, moveOn: true))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Failed, success: false, scaled: 0.9m, moveOn: true))).Status);
        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Failed, success: false, scaled: 0.4m, moveOn: true))).Status);
        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Passed, success: true, scaled: 0.8m, moveOn: true))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Passed, success: true, scaled: 0.9m, moveOn: true))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Failed, success: false, scaled: 0.1m, moveOn: true))).Status);
        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Completed, completion: true, moveOn: true))).Status);
        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Completed, completion: true, moveOn: true))).Status);
    }

    [Fact]
    public async Task ABatchWithOneViolation_StoresNothing()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var au = await AuAsync(h, "ana");

        var refused = await au.PostAsync(new JsonArray(au.Statement(Cmi5Vocabulary.Verbs.Initialized), au.Statement(Cmi5Vocabulary.Verbs.Initialized)));
        var accepted = await au.PostAsync(new JsonArray(au.Statement(Cmi5Vocabulary.Verbs.Initialized), au.Allowed(Answered), au.Statement(Cmi5Vocabulary.Verbs.Terminated)));

        Assert.Equal(403, refused.Status);
        Assert.Equal(200, accepted.Status);
    }

    [Fact]
    public async Task AnotherClient_IsHeldToTheRules_ForCmi5StatementsOfTheRegistration()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var au = await AuAsync(h, "ana");
        var client = XapiTestHarness.Client("tool", write: true);

        Assert.Equal(403, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Completed, completion: true, moveOn: true), client)).Status);
        Assert.Equal(200, (await au.PostAsync(au.Statement(Cmi5Vocabulary.Verbs.Initialized), client)).Status);
    }

    [Fact]
    public async Task AnAbandonedSession_TakesNoMoreStatements_AndAReviewCannotPass()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await AuAsync(h, "ana");

        await first.PostAsync(first.Statement(Cmi5Vocabulary.Verbs.Initialized));
        await h.LaunchAsync("ana");

        Assert.Equal(403, (await first.PostAsync(first.Allowed(Answered))).Status);

        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CrestApps.OrchardCore.Learning.Core.Services.EnrollmentStore>();
            var record = await store.FindByIdAsync(enrollment.ItemId);

            record.Status = EnrollmentStatus.Completed;
            await store.SaveAsync(record);

            return true;
        });

        var review = await AuAsync(h, "ana");

        Assert.Equal(200, (await review.PostAsync(review.Statement(Cmi5Vocabulary.Verbs.Initialized))).Status);
        Assert.Equal(403, (await review.PostAsync(review.Statement(Cmi5Vocabulary.Verbs.Passed, success: true, scaled: 0.9m, moveOn: true))).Status);
    }

    [Fact]
    public async Task Round9Low_ALaunchToken_WritesOnlyIntoItsOwnSession()
    {
        // Arrange: ana launches the first AU, then the second; each launch has a session and a token of its own.
        await using var h = await Cmi5TestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var first = await AuAsync(h, "ana");
        var second = await AuAsync(h, "ana", au: 1);

        // Act: the first launch's token sends a statement of the second session.
        var crossed = await (second with { Credential = first.Credential }).PostAsync(second.Statement(Cmi5Vocabulary.Verbs.Initialized));
        var own = await second.PostAsync(second.Statement(Cmi5Vocabulary.Verbs.Initialized));

        // Assert
        Assert.Equal(403, crossed.Status);
        Assert.Equal(200, own.Status);
    }

    [Fact]
    public async Task Round9Low_AnotherClient_CannotChangeTheLearnersCmi5State_WithAStatementAboutSomeoneElse()
    {
        await using var h = await Cmi5TestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var au = await AuAsync(h, "ana");
        var client = XapiTestHarness.Client("tool", write: true);
        var someoneElse = au with { Actor = XapiSamples.Agent("mailto:someone@example.com") };

        Assert.Equal(403, (await someoneElse.PostAsync(someoneElse.Statement(Cmi5Vocabulary.Verbs.Initialized), client)).Status);
    }

    [Fact]
    public void MoveOn_AndTheItemResult_FollowTheCourse()
    {
        var course = Cmi5TestHarness.SampleCourse();
        var state = new CrestApps.OrchardCore.Learning.Packages.Models.Cmi5State
        {
            Aus =
            [
                new() { AuId = Cmi5TestHarness.FirstAu, Failed = true, ScoreScaled = 0.4m },
                new() { AuId = Cmi5TestHarness.SecondAu, Completed = true },
            ],
        };

        var result = Cmi5Progress.Map(course, state, "item", 3);

        Assert.False(Cmi5Progress.MoveOnMet(course.Aus[0], state.Aus[0]));
        Assert.True(Cmi5Progress.MoveOnMet(course.Aus[1], state.Aus[1]));
        Assert.True(Cmi5Progress.MoveOnMet(new CrestApps.OrchardCore.Learning.Packages.Models.Cmi5Au { MoveOn = "NotApplicable" }, null));
        Assert.True(Cmi5Progress.MoveOnMet(new CrestApps.OrchardCore.Learning.Packages.Models.Cmi5Au { MoveOn = "CompletedOrPassed" }, new() { Completed = true }));
        Assert.False(Cmi5Progress.MoveOnMet(new CrestApps.OrchardCore.Learning.Packages.Models.Cmi5Au { MoveOn = "CompletedAndPassed" }, new() { Completed = true }));
        Assert.False(result.Passed);
        Assert.Equal(40m, result.Score);
        Assert.False(result.ReportedComplete);
        Assert.True(result.SelfReported);
        Assert.Equal(3, result.Attempts);
    }

    private static async Task<AuSession> AuAsync(Cmi5TestHarness h, string userId, int au = 0)
    {
        var launch = await h.LaunchAsync(userId, au);

        Assert.True(launch.Succeeded, launch.Error);

        var fetch = await h.FetchAsync(launch.LaunchUrl);
        var parameters = Cmi5TestHarness.LaunchParameters(launch.LaunchUrl);

        return new AuSession(h, await h.CredentialAsync(fetch), launch.SessionId, launch.Registration, parameters["activityId"], JsonNode.Parse(parameters["actor"]).AsObject(), au == 0 ? Cmi5TestHarness.FirstAu : Cmi5TestHarness.SecondAu);
    }

    private static async Task<List<string>> SatisfiedObjectsAsync(Cmi5TestHarness h, string registration)
    {
        var response = await h.LrsAsync(XapiRequest.Create("GET", "statements", query: new Dictionary<string, string> { ["registration"] = registration, ["verb"] = Cmi5Vocabulary.Verbs.Satisfied }), XapiTestHarness.Admin);

        return JsonNode.Parse(response.Text)["statements"].AsArray().Select(statement => statement["object"]["id"].GetValue<string>()).ToList();
    }

    private sealed record AuSession(Cmi5TestHarness Harness, XapiCredential Credential, string SessionId, string Registration, string ActivityId, JsonObject Actor, string PublisherId)
    {
        public Task<XapiResponse> PostAsync(JsonNode statements, XapiCredential credential = null)
            => Harness.LrsAsync(XapiRequest.Create("POST", "statements", statements), credential ?? Credential);

        public JsonObject Statement(string verb, bool? completion = null, bool? success = null, decimal? scaled = null, bool moveOn = false)
        {
            var categories = new JsonArray(new JsonObject { ["id"] = Cmi5Vocabulary.Categories.Cmi5 });

            if (moveOn)
            {
                categories.Add(new JsonObject { ["id"] = Cmi5Vocabulary.Categories.MoveOn });
            }

            var statement = Base(verb, ActivityId);

            statement["context"]["contextActivities"]["category"] = categories;

            var result = new JsonObject();

            if (completion is { } done)
            {
                result["completion"] = done;
            }

            if (success is { } ok)
            {
                result["success"] = ok;
            }

            if (scaled is { } value)
            {
                result["score"] = new JsonObject { ["scaled"] = value };
            }

            if (result.Count > 0)
            {
                result["duration"] = "PT1M";
                statement["result"] = result;
            }

            return statement;
        }

        public JsonObject Allowed(string verb)
            => Base(verb, ActivityId + "/question/1");

        private JsonObject Base(string verb, string objectId)
            => new()
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["actor"] = Actor.DeepClone(),
                ["verb"] = new JsonObject { ["id"] = verb },
                ["object"] = new JsonObject { ["id"] = objectId },
                ["context"] = new JsonObject
                {
                    ["registration"] = Registration,
                    ["contextActivities"] = new JsonObject
                    {
                        ["grouping"] = new JsonArray(new JsonObject { ["id"] = PublisherId }),
                    },
                    ["extensions"] = new JsonObject { [Cmi5Vocabulary.Extensions.SessionId] = SessionId },
                },
            };
    }
}
