using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

public sealed class SurveyResponseServiceTests
{
    [Fact]
    public async Task Start_SignedIn_CreatesAResponseOnThePublishedVersion_AndResumesIt()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(TwoPages());

        // Act
        var first = await harness.StartAsync(survey, "u1");
        var second = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.True(first.Succeeded);
        Assert.Equal(1, first.Response.VersionNumber);
        Assert.Equal("p1", first.Response.CurrentPageId);
        Assert.StartsWith("R", first.Response.Number, StringComparison.Ordinal);
        Assert.Null(first.ResumeToken);
        Assert.True(second.Resumed);
        Assert.Equal(first.Response.ItemId, second.Response.ItemId);
    }

    [Fact]
    public async Task Start_ClosedOrUnpublished_IsRefused()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var closed = await harness.PublishAsync(TwoPages(), survey => survey.Status = SurveyStatus.Closed);

        // Act
        var result = await harness.StartAsync(closed, "u1");

        // Assert
        Assert.Equal(SurveyErrorCode.Closed, result.Error);
    }

    [Fact]
    public async Task Pages_RequiredAnswer_BlocksNext_ThenNextBackAndFinish()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(TwoPages());
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var invalid = await harness.PostAsync(response, identity, SurveyPageDirection.Next);
        var next = await harness.PostAsync(response, identity, SurveyPageDirection.Next, ("q1", "Ann"));
        var onPage2 = next.Response;
        var back = await harness.PostAsync(onPage2, identity, SurveyPageDirection.Back, ("q2", "draft"));
        var again = await harness.PostAsync(back.Response, identity, SurveyPageDirection.Next, ("q1", "Ann"));
        var finished = await harness.PostAsync(again.Response, identity, SurveyPageDirection.Finish, ("q2", "done"));

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, invalid.Error);
        Assert.Contains("q1", invalid.Errors.Keys);
        Assert.Equal("p2", onPage2.CurrentPageId);
        Assert.Equal("p1", back.Response.CurrentPageId);
        Assert.Equal("draft", back.Response.Answers["q2"].Values[0]);
        Assert.True(finished.Ended);

        var stored = await harness.FindAsync(response.ItemId);

        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.Equal("end", stored.EndingId);
        Assert.Equal("done", stored.Answers["q2"].Values[0]);
        Assert.NotNull(stored.CompletedUtc);
        Assert.All(stored.PageVisits, visit => Assert.NotNull(visit.LeftUtc));
    }

    [Fact]
    public async Task Pages_AnswersOfHiddenQuestions_AreDropped()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var q1 = RuntimeSurveys.Choice("q1", "yes", "no");
        var q2 = RuntimeSurveys.Text("q2");

        q2.Visibility = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "yes");

        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2), RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q3"))));
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var result = await harness.PostAsync(response, identity, SurveyPageDirection.Next, ("q1", "no"), ("q2", "should go"));

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.Response.Answers.ContainsKey("q2"));
        Assert.True(result.Response.Answers.ContainsKey("q1"));
    }

    [Fact]
    public async Task Pages_BranchToEnding_ScreensOut()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var page1 = RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "adult", "minor"));

        page1.Branches.Add(new SurveyBranch
        {
            Id = "b1",
            Rule = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "minor").Rule,
            TargetKind = BranchTargetKind.Ending,
            TargetId = "out",
        });

        var definition = RuntimeSurveys.Definition(page1, RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")));
        definition.Endings.Add(new SurveyEnding { Id = "out", Name = "out", Title = "Sorry", Outcome = EndingOutcome.ScreenedOut });

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var result = await harness.PostAsync(response, identity, SurveyPageDirection.Next, ("q1", "minor"));

        // Assert
        Assert.True(result.Ended);

        var stored = await harness.FindAsync(response.ItemId);

        Assert.Equal(SurveyResponseStatus.ScreenedOut, stored.Status);
        Assert.Equal("out", stored.EndingId);
    }

    [Fact]
    public async Task Pages_BranchToLaterPage_SkipsThePagesBetween()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var page1 = RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "skip", "stay"));

        page1.Branches.Add(new SurveyBranch
        {
            Id = "b1",
            Rule = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "skip").Rule,
            TargetKind = BranchTargetKind.Page,
            TargetId = "p3",
        });

        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(page1, RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")), RuntimeSurveys.Page("p3", RuntimeSurveys.Text("q3"))));
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var result = await harness.PostAsync(response, identity, SurveyPageDirection.Next, ("q1", "skip"));

        // Assert
        Assert.Equal("p3", result.Response.CurrentPageId);
        Assert.Equal(["p1", "p3"], result.Response.PagePath);
    }

    [Fact]
    public async Task Anonymous_StoresNoIdentity_AndNeedsTheResumeToken()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(TwoPages(), item =>
        {
            item.Access.Anonymity = AnonymityMode.Anonymous;
            item.Access.CollectIpAddress = true;
        });

        // Act
        var start = await harness.StartAsync(survey, "u1", request => request.Identity.IpAddress = "10.0.0.1");
        var byUser = await harness.ResponsesAsync(responses => responses.FindForRespondentAsync(start.Response.ItemId, new SurveyRespondentIdentity { UserId = "u1" }));
        var byToken = await harness.ResponsesAsync(responses => responses.FindForRespondentAsync(start.Response.ItemId, new SurveyRespondentIdentity { ResumeToken = start.ResumeToken }));
        var resumed = await harness.StartAsync(survey, "u1", request => request.Identity.ResumeToken = start.ResumeToken);

        // Assert
        Assert.Null(start.Response.UserId);
        Assert.Null(start.Response.IpAddress);
        Assert.NotNull(start.ResumeToken);
        Assert.Null(byUser);
        Assert.NotNull(byToken);
        Assert.True(resumed.Resumed);
    }

    [Fact]
    public async Task Anonymous_KeepsNoPropertiesOrIdentifyingContext_AndParticipationKeepsTheDayOnly()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(
            RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))),
            item => item.Access.Anonymity = AnonymityMode.Anonymous);
        var context = new RecordRef("LearningEnrollment", "enrollment-1");

        // Act
        var start = await harness.StartAsync(survey, "u1", request =>
        {
            request.Context = context;
            request.Properties = new System.Text.Json.Nodes.JsonObject { ["email"] = "person@example.com" };
        });
        var resumed = await harness.StartAsync(survey, "u1", request =>
        {
            request.Context = context;
            request.Identity.ResumeToken = start.ResumeToken;
        });
        await harness.PostAsync(start.Response, new SurveyRespondentIdentity { UserId = "u1", ResumeToken = start.ResumeToken }, SurveyPageDirection.Finish, ("q1", "a"));
        var participation = await harness.RunAsync(async services => await services.GetRequiredService<CrestApps.OrchardCore.Surveys.Core.Services.SurveyParticipationStore>().GetAllAsync());

        // Assert
        Assert.Empty(start.Response.Properties);
        Assert.Equal("LearningEnrollment", start.Response.Context?.Kind);
        Assert.Equal("anonymous", start.Response.Context?.Id);
        Assert.True(resumed.Resumed);
        // One record per start and per completion.
        Assert.Equal(2, participation.Count);
        Assert.All(participation, row =>
        {
            Assert.StartsWith("a:", row.RespondentKey, StringComparison.Ordinal);
            Assert.DoesNotContain("enrollment-1", row.ContextKey, StringComparison.Ordinal);
            Assert.Null(row.LastCompletedUtc);
        });
        Assert.Equal(1, participation.Sum(row => row.CompletedCount));
    }

    [Fact]
    public async Task Anonymous_KeepsTheReturnLink_WithoutItsQueryString_AndDropsEveryOtherProperty()
    {
        // Arrange: a course item starts an anonymous survey with a link back to itself.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(
            RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))),
            item => item.Access.Anonymity = AnonymityMode.Anonymous);

        // Act
        var start = await harness.StartAsync(survey, "u1", request => request.Properties = new System.Text.Json.Nodes.JsonObject
        {
            [SurveyResponseProperties.ReturnUrl] = "/learning/course-1/item-2?enrollment=e-42#top",
            [SurveyResponseProperties.ReturnText] = "Week 1 feedback",
            ["email"] = "person@example.com",
        });
        var external = await harness.StartAsync(survey, "u2", request => request.Properties = new System.Text.Json.Nodes.JsonObject
        {
            [SurveyResponseProperties.ReturnUrl] = "//evil.example/path",
        });

        // Assert
        Assert.Equal("/learning/course-1/item-2", SurveyResponseProperties.ReadString(start.Response, SurveyResponseProperties.ReturnUrl));
        Assert.Equal("Week 1 feedback", SurveyResponseProperties.ReadString(start.Response, SurveyResponseProperties.ReturnText));
        Assert.False(start.Response.Properties.ContainsKey("email"));
        Assert.Empty(external.Response.Properties);
    }

    [Fact]
    public async Task Limits_PerDay_AreCheckedAtStartAndAgainAtCompletion()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(
            RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))),
            item => item.Access.Limits = new RespondentLimits { PerDay = 1, Counts = LimitCount.Completed });

        var identity = new SurveyRespondentIdentity { UserId = "u1" };

        // Two tabs start before either completes; contexts differ so both may start.
        var tab1 = (await harness.StartAsync(survey, "u1")).Response;
        var tab2 = (await harness.StartAsync(survey, "u1", request => request.Context = new RecordRef("Other", "1"))).Response;

        // Act
        var first = await harness.PostAsync(tab1, identity, SurveyPageDirection.Finish, ("q1", "a"));
        var third = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.True(first.Ended);
        Assert.Equal(SurveyErrorCode.LimitReached, third.Error);
        Assert.NotNull(tab2);
    }

    [Fact]
    public async Task Limits_CompletionRecheck_RefusesTheSecondTab()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(
            RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))),
            item =>
            {
                item.Access.Anonymity = AnonymityMode.Anonymous;
                item.Access.Limits = new RespondentLimits { PerDay = 1, Counts = LimitCount.Completed };
            });

        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var tab1 = await harness.StartAsync(survey, "u1");

        // A second browser tab without the resume cookie starts a second response.
        var tab2 = await harness.StartAsync(survey, "u1");

        // Act
        var first = await harness.PostAsync(tab1.Response, new SurveyRespondentIdentity { UserId = "u1", ResumeToken = tab1.ResumeToken }, SurveyPageDirection.Finish, ("q1", "a"));
        var second = await harness.PostAsync(tab2.Response, new SurveyRespondentIdentity { UserId = "u1", ResumeToken = tab2.ResumeToken }, SurveyPageDirection.Finish, ("q1", "b"));

        // Assert
        Assert.NotEqual(tab1.Response.ItemId, tab2.Response.ItemId);
        Assert.True(first.Ended);
        Assert.Equal(SurveyErrorCode.LimitReached, second.Error);
        Assert.Equal(SurveyResponseStatus.InProgress, (await harness.FindAsync(tab2.Response.ItemId)).Status);
        Assert.NotNull(identity);
    }

    [Fact]
    public async Task Deadline_PassedResponse_IsSubmittedAutomatically()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = TwoPages();
        definition.Timing.TimeLimitMinutes = 10;

        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(11);
        var result = await harness.ResponsesAsync(responses => responses.SubmitAtDeadlineAsync(response.ItemId));

        // Assert
        Assert.Equal(RuntimeTestHarness.Now.AddMinutes(10), response.DeadlineUtc);
        Assert.True(result.Succeeded);

        var stored = await harness.FindAsync(response.ItemId);

        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.True(stored.AutoSubmitted);
    }

    [Fact]
    public async Task Post_AfterDeadline_IsRefusedAndSubmits()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = TwoPages();
        definition.Timing.TimeLimitMinutes = 5;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(6);
        var result = await harness.PostAsync(response, identity, SurveyPageDirection.Next, ("q1", "late"));

        // Assert
        Assert.Equal(SurveyErrorCode.DeadlinePassed, result.Error);
        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(response.ItemId)).Status);
    }

    [Fact]
    public async Task Reopen_WhenAllowed_PutsTheResponseBackInProgress()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        definition.Navigation.AllowEditAfterSubmit = true;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await harness.PostAsync(response, identity, SurveyPageDirection.Finish, ("q1", "a"));

        // Act
        var reopened = await harness.ResponsesAsync(responses => responses.ReopenAsync(response.ItemId, identity));
        var stranger = await harness.ResponsesAsync(responses => responses.ReopenAsync(response.ItemId, new SurveyRespondentIdentity { UserId = "u2" }));

        // Assert
        Assert.True(reopened.Succeeded);
        Assert.Equal(SurveyResponseStatus.InProgress, reopened.Response.Status);
        Assert.Equal(SurveyErrorCode.NotOwner, stranger.Error);
    }

    [Fact]
    public async Task Save_KeepsAnswersWithoutValidation_AndStaleTabIsBusy()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(TwoPages());
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var saved = await harness.ResponsesAsync(responses => responses.SaveAsync(new SavePageRequest
        {
            ResponseId = response.ItemId,
            Identity = identity,
            PageId = "p1",
            Form = RuntimeTestHarness.Form(("q1", "partial")),
        }));

        var stale = await harness.ResponsesAsync(responses => responses.SaveAsync(new SavePageRequest
        {
            ResponseId = response.ItemId,
            Identity = identity,
            PageId = "p2",
            Form = RuntimeTestHarness.Form(("q2", "x")),
        }));

        // Assert
        Assert.True(saved.Succeeded);
        Assert.Equal("partial", (await harness.FindAsync(response.ItemId)).Answers["q1"].Values[0]);
        Assert.Equal(SurveyErrorCode.Busy, stale.Error);
    }

    [Fact]
    public async Task Evaluate_ReturnsVisibilityWithoutSaving()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var q1 = RuntimeSurveys.Choice("q1", "yes", "no");
        var q2 = RuntimeSurveys.Text("q2");

        q2.Visibility = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "yes");

        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2)));
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var result = await harness.ResponsesAsync(responses => responses.EvaluateAsync(new EvaluatePageRequest
        {
            ResponseId = response.ItemId,
            Identity = identity,
            Form = RuntimeTestHarness.Form(("q1", "no")),
        }));

        // Assert
        Assert.Contains("q2", result.HiddenQuestionIds);
        Assert.False((await harness.FindAsync(response.ItemId)).Answers.ContainsKey("q1"));
    }

    [Fact]
    public async Task Preview_RunsTheDraft_AsATestResponse()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(TwoPages(), item => item.Draft = RuntimeSurveys.Definition(RuntimeSurveys.Page("draft-page", RuntimeSurveys.Text("d1"))));

        // Act
        var result = await harness.StartAsync(survey, "staff", request =>
        {
            request.IsTest = true;
            request.UseDraft = true;
            request.Identity.IsStaff = true;
        });

        var page = await harness.ResponsesAsync(responses => responses.GetPageAsync(result.Response));

        // Assert
        Assert.True(result.Response.IsTest);
        Assert.Equal(SurveyRuntimeVersions.DraftVersionId, result.Response.SurveyVersionId);
        Assert.Equal("draft-page", page.Page.Id);
    }

    [Fact]
    public async Task Void_ThenDelete_FollowTheTransitions()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(TwoPages());
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var voided = await harness.ResponsesAsync(responses => responses.VoidAsync(response.ItemId, "admin", "duplicate"));
        var again = await harness.ResponsesAsync(responses => responses.VoidAsync(response.ItemId, "admin", "again"));
        var deleted = await harness.ResponsesAsync(responses => responses.DeleteAsync(response.ItemId, "admin"));

        // Assert
        Assert.True(voided.Succeeded);
        Assert.Equal(SurveyErrorCode.InvalidState, again.Error);
        Assert.True(deleted.Succeeded);
        Assert.Null(await harness.FindAsync(response.ItemId));
    }

    [Fact]
    public async Task Observers_HearStartAndCompletion()
    {
        // Arrange
        var observer = new RecordingObserver();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyResponseObserver>(observer));
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        await harness.PostAsync(response, identity, SurveyPageDirection.Finish, ("q1", "a"));

        // Assert
        Assert.Equal(["Started", "Completing", "PageCompleted", "Completed"], observer.Events);
    }

    private static SurveyDefinition TwoPages()
        => RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1", required: true)),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")));

    private sealed class RecordingObserver : ISurveyResponseObserver
    {
        public List<string> Events { get; } = [];

        public Task StartedAsync(SurveyResponseEventContext context)
        {
            Events.Add("Started");

            return Task.CompletedTask;
        }

        public Task PageCompletedAsync(SurveyResponseEventContext context)
        {
            Events.Add("PageCompleted");

            return Task.CompletedTask;
        }

        public Task CompletingAsync(SurveyResponseEventContext context)
        {
            Events.Add("Completing");

            return Task.CompletedTask;
        }

        public Task CompletedAsync(SurveyResponseEventContext context)
        {
            Events.Add("Completed");

            return Task.CompletedTask;
        }
    }
}
