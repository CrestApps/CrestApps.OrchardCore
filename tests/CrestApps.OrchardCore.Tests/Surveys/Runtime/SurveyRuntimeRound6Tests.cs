using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Formulas;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The respondent runtime findings of the sixth review: the close's grace (R6-M1), accommodations kept on the response
/// (R6-M11), attempt numbers and "once per person" (R6-L6, R6-L18), anonymous numbers (R6-L13) and the time rules of a
/// finished response (R6-L3).
/// </summary>
public sealed class SurveyRuntimeRound6Tests
{
    private static readonly SurveyRespondentIdentity _u1 = new() { UserId = "u1" };

    [Fact]
    public async Task Post_WithinTheGraceAfterADeadlineClampedToTheClose_IsTakenWithItsAnswers()
    {
        // Arrange: a 60-minute quiz that closes in 30 minutes, so the deadline is the close.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var closes = RuntimeTestHarness.Now.AddMinutes(30);
        var definition = OnePage();
        definition.Timing.TimeLimitMinutes = 60;
        var survey = await harness.PublishAsync(definition, item => item.Access.ClosesUtc = closes);
        var start = await harness.StartAsync(survey, "u1");

        // Act: the last page, posted by the timer at the deadline, arrives 2 seconds after the close.
        harness.Clock.UtcNow = closes.AddSeconds(2);
        var posted = await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "last answer"));
        var stored = await harness.FindAsync(start.Response.ItemId);

        // Assert: the post is taken with its answer, as the deadline's grace promises.
        Assert.Equal(closes, start.Response.DeadlineUtc);
        Assert.True(posted.Ended, posted.Error.ToString());
        Assert.Equal(SurveyErrorCode.None, posted.Error);
        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.False(stored.AutoSubmitted);
        Assert.Equal("last answer", Assert.Single(stored.Answers["q1"].Values));
    }

    [Fact]
    public async Task Post_AfterTheCloseAndItsGrace_SubmitsOnlyWhatWasSaved()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var closes = RuntimeTestHarness.Now.AddMinutes(30);
        var definition = OnePage();
        definition.Timing.TimeLimitMinutes = 60;
        var survey = await harness.PublishAsync(definition, item => item.Access.ClosesUtc = closes);
        var start = await harness.StartAsync(survey, "u1");

        // Act: 6 seconds after the close, past the 5-second grace.
        harness.Clock.UtcNow = closes.AddSeconds(6);
        var posted = await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "too late"));
        var stored = await harness.FindAsync(start.Response.ItemId);

        // Assert
        Assert.Contains(posted.Error, new[] { SurveyErrorCode.DeadlinePassed, SurveyErrorCode.Closed });
        Assert.True(stored.AutoSubmitted);
        Assert.False(stored.Answers.ContainsKey("q1"));
    }

    [Fact]
    public async Task Start_ClampsTheDeadlineToTheLinksEarlierClose()
    {
        // Arrange: the survey closes in 2 hours, but its link closes in 20 minutes.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = OnePage();
        definition.Timing.TimeLimitMinutes = 60;
        var survey = await harness.PublishAsync(definition, item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(2));
        var linkCloses = RuntimeTestHarness.Now.AddMinutes(20);

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyCollectorStore>();
            var collector = await store.FindDefaultAsync(survey.ItemId);
            collector.ClosesUtc = linkCloses;
            await store.UpdateAsync(collector);

            return true;
        });

        // Act
        var start = await harness.StartAsync(survey, "u1");

        // Assert: the timer shows the real cut-off.
        Assert.True(start.Succeeded, start.Error.ToString());
        Assert.Equal(linkCloses, start.Response.DeadlineUtc);
    }

    [Fact]
    public async Task Upkeep_AnAnonymousResponse_KeepsItsAccommodationsLaterClose()
    {
        // Arrange (R6-M11, scenario A): an anonymous survey closes in an hour; u1's accommodation closes two days later.
        var overrides = new FakeOverrides();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyOverrideService>(overrides));
        var survey = await harness.PublishAsync(OnePage(), item =>
        {
            item.Access.Anonymity = AnonymityMode.Anonymous;
            item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(1);
        });
        overrides.Items.Add(new SurveyOverride { ItemId = "o1", SurveyId = survey.ItemId, UserId = "u1", ClosesUtc = RuntimeTestHarness.Now.AddDays(2), Reason = "Extra time" });
        var start = await harness.StartAsync(survey, "u1");

        // Act: upkeep runs after the survey's close, and again after the accommodation's.
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(2);
        await RunUpkeepAsync(harness);
        var afterSurveyClose = await harness.FindAsync(start.Response.ItemId);

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(3);
        await RunUpkeepAsync(harness);
        var afterOwnClose = await harness.FindAsync(start.Response.ItemId);

        // Assert: the response keeps no user, yet it stays open until its respondent's own close.
        Assert.Null(start.Response.UserId);
        Assert.Equal(RuntimeTestHarness.Now.AddDays(2), start.Response.Accommodation?.ClosesUtc);
        Assert.Equal(SurveyResponseStatus.InProgress, afterSurveyClose.Status);
        Assert.Equal(SurveyResponseStatus.Completed, afterOwnClose.Status);
        Assert.True(afterOwnClose.AutoSubmitted);
        Assert.Null(afterOwnClose.Accommodation);
    }

    [Fact]
    public async Task DeadlineSubmit_AnAnonymousExtraAttempt_IsCompleted_NotAbandoned()
    {
        // Arrange (R6-M11, scenario B): once per person, plus one extra attempt from u1's accommodation; timed.
        var overrides = new FakeOverrides();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyOverrideService>(overrides));
        var definition = OnePage();
        definition.Timing.TimeLimitMinutes = 1;
        definition.Timing.GraceSeconds = 0;
        var survey = await harness.PublishAsync(definition, item =>
        {
            item.Access.Anonymity = AnonymityMode.Anonymous;
            item.Access.Limits = new RespondentLimits { Total = 1, Counts = LimitCount.Completed };
        });
        overrides.Items.Add(new SurveyOverride { ItemId = "o1", SurveyId = survey.ItemId, UserId = "u1", ExtraAttempts = 1, Reason = "Retake" });

        var first = await harness.StartAsync(survey, "u1");
        var finished = await harness.PostAsync(first.Response, new SurveyRespondentIdentity { UserId = "u1", ResumeToken = first.ResumeToken }, SurveyPageDirection.Finish, ("q1", "a"));
        var extra = await harness.StartAsync(survey, "u1");

        // Act: the extra attempt runs out of time and the deadline task submits it, with no respondent's request.
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(2);
        var submitted = await harness.ResponsesAsync(responses => responses.SubmitAtDeadlineAsync(extra.Response.ItemId));
        var stored = await harness.FindAsync(extra.Response.ItemId);

        // Assert: the extra attempt is completed, not abandoned unscored.
        Assert.True(finished.Ended);
        Assert.True(extra.Succeeded, extra.Error.ToString());
        Assert.Equal(1, extra.Response.Accommodation?.ExtraAttempts);
        Assert.True(submitted.Succeeded, submitted.Error.ToString());
        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.True(stored.AutoSubmitted);
        Assert.Null(stored.Accommodation);
    }

    [Fact]
    public async Task AttemptNumber_CountsAVoidedAttempt()
    {
        // Arrange (R6-L6): two attempts; the first is voided, and stays used.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { Total = 2, Counts = LimitCount.Started });
        var first = await harness.StartAsync(survey, "u1");
        await harness.PostAsync(first.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));
        await harness.ResponsesAsync(responses => responses.VoidAsync(first.Response.ItemId, "staff", "Misconduct"));

        // Act
        var second = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.True(second.Succeeded, second.Error.ToString());
        Assert.Equal(1, first.Response.AttemptNumber);
        Assert.Equal(2, second.Response.AttemptNumber);
    }

    [Fact]
    public async Task Total_OnAnIdentifiedSurvey_HoldsAfterRetentionAnonymizesTheResponse()
    {
        // Arrange (R6-L18): once per person; retention later takes the user off the completed response.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { Total = 1, Counts = LimitCount.Completed });
        var start = await harness.StartAsync(survey, "u1");
        await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyResponseStore>();
            var response = await store.FindByIdAsync(start.Response.ItemId);
            SurveyMaintenanceService.Anonymize(response);
            await store.UpdateAsync(response);

            return true;
        });

        // Act
        var again = await harness.StartAsync(survey, "u1");

        // Assert: the participation the survey keeps still counts the completion.
        Assert.Null((await harness.FindAsync(start.Response.ItemId)).UserId);
        Assert.Equal(SurveyErrorCode.LimitReached, again.Error);
    }

    [Fact]
    public async Task Delete_GivesTheAttemptBack_InEveryWindow()
    {
        // Arrange: once a day; the day's completion is deleted by staff.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { Total = 1, PerDay = 1, Counts = LimitCount.Completed });
        var start = await harness.StartAsync(survey, "u1");
        await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));
        var refused = await harness.StartAsync(survey, "u1");

        // Act
        await harness.ResponsesAsync(responses => responses.DeleteAsync(start.Response.ItemId, "staff"));
        var again = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.Equal(SurveyErrorCode.LimitReached, refused.Error);
        Assert.True(again.Succeeded, again.Error.ToString());
    }

    [Fact]
    public async Task Start_AnAnonymousResponse_TakesNoNumberFromTheSiteSequence()
    {
        // Arrange (R6-L13)
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var anonymous = await harness.PublishAsync(OnePage(), item => item.Access.Anonymity = AnonymityMode.Anonymous);
        var identified = await harness.PublishAsync(OnePage());

        // Act
        var hidden = await harness.StartAsync(anonymous, "u1");
        var named = await harness.StartAsync(identified, "u1");

        // Assert
        Assert.Null((await harness.FindAsync(hidden.Response.ItemId)).Number);
        Assert.False(string.IsNullOrEmpty(named.Response.Number));
        Assert.Equal(hidden.Response.ItemId[..CrestApps.OrchardCore.Surveys.Core.Access.SurveyAnonymity.AnonymousLabelLength], CrestApps.OrchardCore.Surveys.Core.Access.SurveyAnonymity.ResponseLabel(anonymous, hidden.Response));
    }

    [Fact]
    public async Task Formulas_OfAFinishedResponse_ReadItsCompletionTime_InTheSitesTimeZone()
    {
        // Arrange (R6-L3): the site is in Los Angeles. The response was completed at 17:00 on 22 November there (the
        // 23rd in UTC); the birthday is the 23rd. A regrade runs a week later.
        var losAngeles = OperatingSystem.IsWindows() ? "Pacific Standard Time" : "America/Los_Angeles";
        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            var site = new Mock<ISite>();
            site.SetupGet(value => value.TimeZoneId).Returns(losAngeles);
            var siteService = new Mock<ISiteService>();
            siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
            services.RemoveAll<ISiteService>();
            services.AddSingleton(siteService.Object);
            services.AddSurveysScoring();
        });
        harness.Clock.UtcNow = new DateTime(2026, 11, 30, 12, 0, 0, DateTimeKind.Utc);
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("born")));
        var finished = new SurveyResponse { Status = SurveyResponseStatus.Completed, CompletedUtc = new DateTime(2026, 11, 23, 1, 0, 0, DateTimeKind.Utc) };
        var open = new SurveyResponse { Status = SurveyResponseStatus.InProgress };
        finished.Answers["born"] = new SurveyAnswer { Values = ["2008-11-23"] };
        open.Answers["born"] = new SurveyAnswer { Values = ["2008-11-23"] };

        // Act: the scoring engine's preparation (what a regrade runs) loads the site's time zone.
        var (atCompletion, now) = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<ISurveyScoringEngine>().PrepareAsync();
            var formulas = services.GetRequiredService<SurveyFormulaService>();

            return (formulas.Evaluate(definition, finished, "AGE([born])"), formulas.Evaluate(definition, open, "AGE([born])"));
        });

        // Assert: the finished response is still 17, as at its completion; a response in progress reads now.
        Assert.Equal(17, Convert.ToInt32(atCompletion, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(18, Convert.ToInt32(now, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static SurveyDefinition OnePage()
        => RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));

    private static Task<bool> RunUpkeepAsync(RuntimeTestHarness harness)
        => harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

    private sealed class FakeOverrides : ISurveyOverrideService
    {
        public List<SurveyOverride> Items { get; } = [];

        public Task<SurveyErrorCode> GrantAsync(SurveyOverride surveyOverride, CancellationToken cancellationToken = default)
            => Task.FromResult(SurveyErrorCode.None);

        public Task<IReadOnlyList<SurveyOverride>> ListAsync(string surveyId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SurveyOverride>>(Items.Where(item => item.SurveyId == surveyId).ToList());

        public Task<bool> RevokeAsync(string overrideId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<SurveyOverride> FindEffectiveAsync(string surveyId, string userId, RecordRef? context, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(item => item.SurveyId == surveyId && userId is not null && item.UserId == userId));
    }
}
