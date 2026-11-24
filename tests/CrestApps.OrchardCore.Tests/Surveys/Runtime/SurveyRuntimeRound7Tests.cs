using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The respondent runtime findings of the seventh review: an accommodation revoked while a response is in progress
/// (R7-M5).
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
