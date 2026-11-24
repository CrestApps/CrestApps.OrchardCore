using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The respondent runtime findings of the seventh review: an accommodation revoked while a response is in progress
/// (R7-M5, R7-Mi4), attempt numbers after a delete or retention (R7-Mi1), the closing grace's bound and the review after
/// close (R7-Mi2, R7-Mi3), a staff close during the grace, and a start racing a delete (R7-Mi13).
/// </summary>
public sealed class SurveyRuntimeRound7Tests
{
    private static readonly SurveyRespondentIdentity _u1 = new() { UserId = "u1" };

    [Fact]
    public async Task Upkeep_AfterAnAccommodationIsRevokedMidResponse_SubmitsAtTheSurveysClose()
    {
        // Arrange (R7-M5): u1 is given a close 7 days later; the response stores it; staff then delete the accommodation.
        var overrides = new RevocableOverrides();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyOverrideService>(overrides));
        var closes = RuntimeTestHarness.Now.AddHours(1);
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.ClosesUtc = closes);
        overrides.Items.Add(new SurveyOverride { ItemId = "o1", SurveyId = survey.ItemId, UserId = "u1", ClosesUtc = RuntimeTestHarness.Now.AddDays(7), Reason = "By mistake" });
        var start = await harness.StartAsync(survey, "u1");

        overrides.Items.Clear();

        // Act: upkeep runs past the survey's close and its grace.
        harness.Clock.UtcNow = closes.AddMinutes(10);
        await RunUpkeepAsync(harness);
        var stored = await harness.FindAsync(start.Response.ItemId);

        // Assert: the stored close no longer applies to the identified respondent.
        Assert.Equal(RuntimeTestHarness.Now.AddDays(7), start.Response.Accommodation?.ClosesUtc);
        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.True(stored.AutoSubmitted);
    }

    [Fact]
    public async Task Post_AfterAnAccommodationIsRevokedMidResponse_IsRefusedPastTheSurveysClose()
    {
        // Arrange (R7-M5)
        var overrides = new RevocableOverrides();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyOverrideService>(overrides));
        var closes = RuntimeTestHarness.Now.AddHours(1);
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.ClosesUtc = closes);
        overrides.Items.Add(new SurveyOverride { ItemId = "o1", SurveyId = survey.ItemId, UserId = "u1", ClosesUtc = RuntimeTestHarness.Now.AddDays(7), Reason = "By mistake" });
        var start = await harness.StartAsync(survey, "u1");

        overrides.Items.Clear();

        // Act
        harness.Clock.UtcNow = closes.AddMinutes(10);
        var posted = await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "late"));
        var stored = await harness.FindAsync(start.Response.ItemId);

        // Assert
        Assert.Contains(posted.Error, new[] { SurveyErrorCode.Closed, SurveyErrorCode.DeadlinePassed });
        Assert.False(stored.Answers.ContainsKey("q1"));
    }

    [Fact]
    public async Task Finish_AfterAnExtraAttemptIsRevokedMidResponse_IsRefusedByTheLimit()
    {
        // Arrange (R7-M5): once per person; u1's accommodation gives one extra attempt, which u1 starts; staff then
        // delete the accommodation.
        var overrides = new RevocableOverrides();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyOverrideService>(overrides));
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { Total = 1, Counts = LimitCount.Completed });
        overrides.Items.Add(new SurveyOverride { ItemId = "o1", SurveyId = survey.ItemId, UserId = "u1", ExtraAttempts = 1, Reason = "By mistake" });

        var first = await harness.StartAsync(survey, "u1");
        await harness.PostAsync(first.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));
        var extra = await harness.StartAsync(survey, "u1");

        overrides.Items.Clear();

        // Act
        var finished = await harness.PostAsync(extra.Response, _u1, SurveyPageDirection.Finish, ("q1", "b"));
        var stored = await harness.FindAsync(extra.Response.ItemId);

        // Assert: the extra attempt kept on the response no longer counts.
        Assert.True(extra.Succeeded, extra.Error.ToString());
        Assert.Equal(1, extra.Response.Accommodation?.ExtraAttempts);
        Assert.Equal(SurveyErrorCode.LimitReached, finished.Error);
        Assert.NotEqual(SurveyResponseStatus.Completed, stored.Status);
    }

    [Fact]
    public async Task Post_OnAnAnonymousSurveyWithAHiddenContext_KeepsTheStoredClose()
    {
        // Arrange (R7-M5, the exception): an anonymous survey keeps the kind of an enrollment context only, so the lookup
        // can't match u1's accommodation for that enrollment; the close the start granted stays.
        var overrides = new RevocableOverrides();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyOverrideService>(overrides));
        var closes = RuntimeTestHarness.Now.AddHours(1);
        var enrollment = new RecordRef("enrollment", "e1");
        var survey = await harness.PublishAsync(OnePage(), item =>
        {
            item.Access.Anonymity = AnonymityMode.Anonymous;
            item.Access.ClosesUtc = closes;
        });
        overrides.Items.Add(new SurveyOverride { ItemId = "o1", SurveyId = survey.ItemId, UserId = "u1", Context = enrollment, ClosesUtc = RuntimeTestHarness.Now.AddDays(2), Reason = "Extra time" });
        var start = await harness.StartAsync(survey, "u1", request => request.Context = enrollment);

        // Act: u1 posts after the survey's close, before the accommodation's.
        harness.Clock.UtcNow = closes.AddMinutes(10);
        var posted = await harness.PostAsync(start.Response, new SurveyRespondentIdentity { UserId = "u1", ResumeToken = start.ResumeToken }, SurveyPageDirection.Finish, ("q1", "on time"));

        // Assert
        Assert.Equal(SurveyAnonymity.HiddenContextId, start.Response.Context?.Id);
        Assert.True(posted.Ended, posted.Error.ToString());
        Assert.Equal(SurveyErrorCode.None, posted.Error);
    }

    [Fact]
    public async Task AttemptNumber_AfterStaffDeleteAnEarlierAttempt_IsNotTakenAgain()
    {
        // Arrange (R7-Mi1): attempts 1, 2 and 3; staff delete attempt 2.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { Total = 5, Counts = LimitCount.Started });
        var attempts = new List<SurveyResponse>();

        for (var index = 0; index < 3; index++)
        {
            var start = await harness.StartAsync(survey, "u1");
            await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));
            attempts.Add(start.Response);
        }

        await harness.ResponsesAsync(responses => responses.DeleteAsync(attempts[1].ItemId, "staff"));

        // Act
        var next = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.Equal([1, 2, 3], attempts.Select(attempt => attempt.AttemptNumber));
        Assert.True(next.Succeeded, next.Error.ToString());
        Assert.Equal(4, next.Response.AttemptNumber);
    }

    [Fact]
    public async Task AttemptNumber_AfterRetentionAnonymizesTheResponses_DoesNotRestartAtOne()
    {
        // Arrange (R7-Mi1): a total limit keeps the participation; retention takes the user off the first attempt.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { Total = 3, Counts = LimitCount.Completed });
        var first = await harness.StartAsync(survey, "u1");
        await harness.PostAsync(first.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyResponseStore>();
            var response = await store.FindByIdAsync(first.Response.ItemId);
            SurveyMaintenanceService.Anonymize(response);
            await store.UpdateAsync(response);

            return true;
        });

        // Act
        var next = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.True(next.Succeeded, next.Error.ToString());
        Assert.Equal(2, next.Response.AttemptNumber);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(30, 30)]
    [InlineData(100_000, SurveyAvailability.MaxGraceSeconds)]
    public void GraceSeconds_IsKeptWithinItsBound_WhateverTheDefinitionSays(int stored, int expected)
    {
        // Arrange (R7-Mi2): a recipe or an import can store any value.
        var definition = OnePage();
        definition.Timing.GraceSeconds = stored;

        // Act
        var grace = SurveyAvailability.GraceSeconds(definition);

        // Assert
        Assert.Equal(expected, grace);
    }

    [Theory]
    [InlineData(10, SurveyReviewMoment.LaterWhileOpen)]
    [InlineData(31, SurveyReviewMoment.AfterClose)]
    public void ReviewMoment_AfterClose_StartsOnceTheGracePastTheCloseIsOver(int secondsAfterClose, SurveyReviewMoment expected)
    {
        // Arrange (R7-Mi3): a 30-second grace; another respondent's last page can still be taken until it ends.
        var closes = RuntimeTestHarness.Now.AddHours(1);
        var survey = new Survey { Status = SurveyStatus.Open, Access = new SurveyAccessSettings { ClosesUtc = closes } };
        var definition = OnePage();
        definition.Timing.GraceSeconds = 30;
        var response = new SurveyResponse { Status = SurveyResponseStatus.Completed, CompletedUtc = RuntimeTestHarness.Now };

        // Act
        var moment = new SurveyReviewPolicy().GetMoment(survey, definition, response, closes.AddSeconds(secondsAfterClose));

        // Assert
        Assert.Equal(expected, moment);
    }

    [Fact]
    public async Task Post_DuringTheGrace_AfterStaffCloseTheSurvey_IsRefused()
    {
        // Arrange: a 30-second grace past the close; staff close the survey 2 seconds after its closing date.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var closes = RuntimeTestHarness.Now.AddMinutes(30);
        var definition = OnePage();
        definition.Timing.GraceSeconds = 30;
        var survey = await harness.PublishAsync(definition, item => item.Access.ClosesUtc = closes);
        var start = await harness.StartAsync(survey, "u1");

        harness.Clock.UtcNow = closes.AddSeconds(2);
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var stored = await store.FindByIdAsync(survey.ItemId);
            stored.Status = SurveyStatus.Closed;
            await store.UpdateAsync(stored);

            return true;
        });

        // Act
        var posted = await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "after the staff close"));
        var response = await harness.FindAsync(start.Response.ItemId);

        // Assert: a close by staff has no grace, and the review after close starts with it.
        Assert.Equal(SurveyErrorCode.Closed, posted.Error);
        Assert.False(response.Answers.ContainsKey("q1"));
        Assert.True(SurveyAvailability.IsClosedForReview(new Survey { Status = SurveyStatus.Closed, Access = new SurveyAccessSettings { ClosesUtc = closes } }, definition, closes.AddSeconds(2)));
    }

    [Fact]
    public async Task Start_ThatReadTheSurveyBeforeADeleteMarkedIt_WritesNoResponse()
    {
        // Arrange (R7-Mi13): the start reads the survey; a delete marks it (committed on its own) before the start writes.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage());
        var store = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IStore>()));

        // Act: the scope's session loads the survey first, as the start does, then the mark lands, then the start runs.
        var result = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyStore>().FindByIdAsync(survey.ItemId);

            await using (var marking = store.CreateSession())
            {
                var marked = await marking.Query<Survey, SurveyIndex>(index => index.ItemId == survey.ItemId, SurveysConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
                marked.DeletingUtc = RuntimeTestHarness.Now;
                await marking.SaveAsync(marked, false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
                await marking.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            return await services.GetRequiredService<ISurveyResponseService>().StartAsync(new StartResponseRequest { SurveyId = survey.ItemId, Identity = new SurveyRespondentIdentity { UserId = "u1" } });
        });
        var responses = await harness.RunAsync(services => services.GetRequiredService<ISession>()
            .QueryIndex<SurveyResponseIndex>(index => index.SurveyId == survey.ItemId, collection: SurveysConstants.ResponsesCollectionName)
            .CountAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(SurveyErrorCode.NotFound, result.Error);
        Assert.Equal(0, responses);
    }

    private static SurveyDefinition OnePage()
        => RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));

    private static Task<bool> RunUpkeepAsync(RuntimeTestHarness harness)
        => harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

    // Accommodations staff can delete, matched as the real service matches them: one scoped to a context applies only to
    // a lookup with that context.
    private sealed class RevocableOverrides : ISurveyOverrideService
    {
        public List<SurveyOverride> Items { get; } = [];

        public Task<SurveyErrorCode> GrantAsync(SurveyOverride surveyOverride, CancellationToken cancellationToken = default)
            => Task.FromResult(SurveyErrorCode.None);

        public Task<IReadOnlyList<SurveyOverride>> ListAsync(string surveyId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SurveyOverride>>(Items.Where(item => item.SurveyId == surveyId).ToList());

        public Task<bool> RevokeAsync(string overrideId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.RemoveAll(item => item.ItemId == overrideId) > 0);

        public Task<SurveyOverride> FindEffectiveAsync(string surveyId, string userId, RecordRef? context, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(item => item.SurveyId == surveyId
                && userId is not null
                && item.UserId == userId
                && (item.Context is null || (context is not null && item.Context.Value.Equals(context.Value)))));
    }
}
