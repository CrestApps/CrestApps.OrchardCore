using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// A closed survey takes no new answers (P1-H1): a passed closing date counts as a close everywhere, a response in
/// progress is submitted as it was saved, and nothing is reopened. A submission for the respondent runs the completion
/// rules and ends abandoned when they refuse (P1-M1). The total limit never forgets (P1-H3).
/// </summary>
public sealed class SurveyCloseAndAutoSubmitTests
{
    private static readonly SurveyRespondentIdentity _u1 = new() { UserId = "u1" };

    [Fact]
    public async Task Reopen_AfterTheClosingDate_IsRefused()
    {
        // Arrange: changes after finishing are allowed, and the survey closes in an hour.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = OnePage();
        definition.Navigation.AllowEditAfterSubmit = true;

        var survey = await harness.PublishAsync(definition, item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(1));
        var start = await harness.StartAsync(survey, "u1");
        var finished = await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));

        // Act: the closing date passed, but nobody closed the survey.
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(2);
        var reopened = await harness.ResponsesAsync(responses => responses.ReopenAsync(start.Response.ItemId, _u1));

        // Assert
        Assert.True(finished.Ended);
        Assert.Equal(SurveyErrorCode.Closed, reopened.Error);
        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(start.Response.ItemId)).Status);
    }

    [Fact]
    public async Task Reopen_BeforeTheClosingDate_IsAllowed()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = OnePage();
        definition.Navigation.AllowEditAfterSubmit = true;

        var survey = await harness.PublishAsync(definition, item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(1));
        var start = await harness.StartAsync(survey, "u1");
        await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));

        // Act
        var reopened = await harness.ResponsesAsync(responses => responses.ReopenAsync(start.Response.ItemId, _u1));

        // Assert
        Assert.True(reopened.Succeeded);
        Assert.Equal(SurveyResponseStatus.InProgress, (await harness.FindAsync(start.Response.ItemId)).Status);
    }

    [Fact]
    public async Task Post_AfterTheClosingDate_SubmitsWhatWasSaved_AndIgnoresTheNewAnswers()
    {
        // Arrange: an answer saved before the close.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(1));
        var start = await harness.StartAsync(survey, "u1");

        await harness.ResponsesAsync(responses => responses.SaveAsync(new SavePageRequest
        {
            ResponseId = start.Response.ItemId,
            Identity = _u1,
            PageId = start.Response.CurrentPageId,
            Form = RuntimeTestHarness.Form(("q1", "saved")),
        }));

        // Act: after the close, the respondent posts other answers (read from someone's review after close, say).
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(2);
        var posted = await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "new"));
        var stored = await harness.FindAsync(start.Response.ItemId);

        // Assert
        Assert.Equal(SurveyErrorCode.Closed, posted.Error);
        Assert.True(posted.Ended);
        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.True(stored.AutoSubmitted);
        Assert.Equal("saved", Assert.Single(stored.Answers["q1"].Values));
    }

    [Fact]
    public async Task Save_AfterStaffCloseTheSurvey_SavesNothingNew_AndSubmitsTheResponse()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage());
        var start = await harness.StartAsync(survey, "u1");

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var stored = await store.FindByIdAsync(survey.ItemId);

            stored.Status = SurveyStatus.Closed;
            await store.UpdateAsync(stored);

            return true;
        });

        // Act
        var saved = await harness.ResponsesAsync(responses => responses.SaveAsync(new SavePageRequest
        {
            ResponseId = start.Response.ItemId,
            Identity = _u1,
            PageId = start.Response.CurrentPageId,
            Form = RuntimeTestHarness.Form(("q1", "after close")),
        }));
        var response = await harness.FindAsync(start.Response.ItemId);

        // Assert
        Assert.Equal(SurveyErrorCode.Closed, saved.Error);
        Assert.Equal(SurveyResponseStatus.Completed, response.Status);
        Assert.False(response.Answers.ContainsKey("q1"));
    }

    [Fact]
    public async Task Upkeep_SubmitsTheResponsesLeftInProgressOnAClosedSurvey()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(1));
        var open = await harness.PublishAsync(OnePage());
        var closing = await harness.StartAsync(survey, "u1");
        var staying = await harness.StartAsync(open, "u1");

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(2);
        await harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        var submitted = await harness.FindAsync(closing.Response.ItemId);

        Assert.Equal(SurveyResponseStatus.Completed, submitted.Status);
        Assert.True(submitted.AutoSubmitted);
        Assert.Equal(SurveyResponseStatus.InProgress, (await harness.FindAsync(staying.Response.ItemId)).Status);
    }

    [Fact]
    public async Task Start_AfterTheClosingDate_SubmitsTheResponseInProgress_AndRefusesTheStart()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(1));
        var start = await harness.StartAsync(survey, "u1");

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(2);
        var again = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.Equal(SurveyErrorCode.Closed, again.Error);
        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(start.Response.ItemId)).Status);
    }

    [Fact]
    public async Task DeadlineSubmit_TheLimitsRefuse_EndsTheResponseAbandoned_AndCountsNothing()
    {
        // Arrange: one completion a day, and a second tab that started a second response.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = OnePage();
        definition.Timing.TimeLimitMinutes = 1;
        definition.Timing.GraceSeconds = 0;

        var survey = await harness.PublishAsync(definition, item =>
        {
            item.Access.Anonymity = AnonymityMode.Anonymous;
            item.Access.Limits = new RespondentLimits { PerDay = 1, Counts = LimitCount.Completed };
        });
        var tab1 = await harness.StartAsync(survey, "u1");
        var tab2 = await harness.StartAsync(survey, "u1");
        var first = await harness.PostAsync(tab1.Response, new SurveyRespondentIdentity { UserId = "u1", ResumeToken = tab1.ResumeToken }, SurveyPageDirection.Finish, ("q1", "a"));

        // Act: the second tab waits for its deadline instead of submitting.
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(2);
        var submitted = await harness.ResponsesAsync(responses => responses.SubmitAtDeadlineAsync(tab2.Response.ItemId));
        var second = await harness.FindAsync(tab2.Response.ItemId);
        var counts = await harness.RunAsync(services => services.GetRequiredService<SurveyParticipationService>().CountAsync(survey, tab2.Response.RespondentKey, null, harness.Clock.UtcNow));

        // Assert
        Assert.True(first.Ended);
        Assert.True(submitted.Succeeded);
        Assert.Equal(SurveyResponseStatus.Abandoned, second.Status);
        Assert.Null(second.CompletedUtc);
        Assert.Null(second.RespondentKey);
        Assert.Equal(1, counts.Completed.Day);
    }

    [Fact]
    public async Task Total_OnAnAnonymousSurvey_CountsACompletionOlderThanTheParticipationRetention()
    {
        // Arrange: once per person, counting completed responses.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item =>
        {
            item.Access.Anonymity = AnonymityMode.Anonymous;
            item.Access.Limits = new RespondentLimits { Total = 1, Counts = LimitCount.Completed };
        });
        var start = await harness.StartAsync(survey, "u1");
        await harness.PostAsync(start.Response, new SurveyRespondentIdentity { UserId = "u1", ResumeToken = start.ResumeToken }, SurveyPageDirection.Finish, ("q1", "a"));

        // Act: 60 days later, after upkeep ran.
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(SurveyParticipationService.RetentionDays + 15);
        await harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });
        var again = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.Equal(SurveyErrorCode.LimitReached, again.Error);
    }

    [Fact]
    public async Task Upkeep_DeletesOldParticipationOfSurveysWithoutATotalLimit_AndKeepsTheOthers()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var withTotal = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { Total = 1 });
        var withoutTotal = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { PerDay = 1 });
        var old = DateOnly.FromDateTime(RuntimeTestHarness.Now).AddDays(-SurveyParticipationService.RetentionDays - 5);

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyParticipationStore>();

            await store.CreateAsync(new SurveyParticipation { SurveyId = withTotal.ItemId, RespondentKey = "k", Day = old, CompletedCount = 1 });
            await store.CreateAsync(new SurveyParticipation { SurveyId = withoutTotal.ItemId, RespondentKey = "k", Day = old, CompletedCount = 1 });

            return true;
        });

        // Act
        await harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });
        var counts = await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyParticipationStore>();

            return (
                Kept: (await store.ListAsync(withTotal.ItemId, "k", DateOnly.MinValue)).Count,
                Deleted: (await store.ListAsync(withoutTotal.ItemId, "k", DateOnly.MinValue)).Count);
        });

        // Assert
        Assert.Equal(1, counts.Kept);
        Assert.Equal(0, counts.Deleted);
    }

    [Fact]
    public async Task Total_OnAnIdentifiedSurvey_CountsCompletedResponses_WhateverTheirAge()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { Total = 1, Counts = LimitCount.Completed });
        var start = await harness.StartAsync(survey, "u1");
        await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));

        // Act: the participation records are gone, as after the retention on a survey without a total limit.
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyParticipationStore>();

            foreach (var row in await store.ListAsync(survey.ItemId, "u:u1", DateOnly.MinValue))
            {
                await store.DeleteAsync(row);
            }

            return true;
        });
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(60);
        var again = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.Equal(SurveyErrorCode.LimitReached, again.Error);
    }

    [Fact]
    public async Task Invitation_DoesNotResumeAResponseStartedThroughThePublicLink()
    {
        // Arrange: a response started through the public link, then the person's invitation.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage());
        var publicStart = await harness.StartAsync(survey, "u1");
        var token = "invitation-token";

        await harness.RunAsync(async services =>
        {
            var collector = await services.GetRequiredService<SurveyCollectorStore>().FindDefaultAsync(survey.ItemId);

            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation
            {
                SurveyId = survey.ItemId,
                CollectorId = collector.ItemId,
                TokenHash = CrestApps.OrchardCore.Surveys.Core.Access.SurveyRespondentKeys.Hash(token),
                Status = SurveyInvitationStatus.Sent,
            });

            return true;
        });

        // Act
        var invited = await harness.StartAsync(survey, "u1", request => request.InvitationToken = token);

        // Assert
        Assert.True(invited.Succeeded);
        Assert.NotEqual(publicStart.Response.ItemId, invited.Response.ItemId);
        Assert.False(string.IsNullOrEmpty(invited.Response.InvitationId));
    }

    [Theory]
    [InlineData(LimitCount.Completed)]
    [InlineData(LimitCount.Started)]
    public async Task Total_AVoidedAttemptStaysUsed_AndOnlyADeleteGivesItBack(LimitCount counts)
    {
        // Arrange: an identified quiz with one attempt; the attempt is voided for misconduct.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage(), item => item.Access.Limits = new RespondentLimits { Total = 1, Counts = counts });
        var start = await harness.StartAsync(survey, "u1");
        await harness.PostAsync(start.Response, _u1, SurveyPageDirection.Finish, ("q1", "a"));
        var voided = await harness.ResponsesAsync(responses => responses.VoidAsync(start.Response.ItemId, "staff", "Misconduct"));

        // Act
        var afterVoid = await harness.StartAsync(survey, "u1");
        await harness.ResponsesAsync(responses => responses.DeleteAsync(start.Response.ItemId, "staff"));
        var afterDelete = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.True(voided.Succeeded, voided.Error.ToString());
        Assert.Equal(SurveyErrorCode.LimitReached, afterVoid.Error);
        Assert.True(afterDelete.Succeeded, afterDelete.Error.ToString());
    }

    [Fact]
    public async Task Upkeep_AccommodatedResponsesOfAClosedSurvey_DoNotUseTheCloseStepsBudget()
    {
        // Arrange: survey A closed, but u1's accommodation keeps it open for another day; survey B closed too. A run
        // handles one response.
        var overrides = new FakeOverrides();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyOverrideService>(overrides));
        var surveyA = await harness.PublishAsync(OnePage(), item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(1));
        var surveyB = await harness.PublishAsync(OnePage(), item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(1));
        overrides.Items.Add(new SurveyOverride { ItemId = "o1", SurveyId = surveyA.ItemId, UserId = "u1", ClosesUtc = RuntimeTestHarness.Now.AddDays(1) });
        overrides.Items.Add(new SurveyOverride { ItemId = "o2", SurveyId = surveyA.ItemId, AudienceId = "group", ClosesUtc = RuntimeTestHarness.Now.AddDays(1) });
        overrides.AudienceMembers.Add("u3");

        var accommodated = await harness.StartAsync(surveyA, "u1");
        var byAudience = await harness.StartAsync(surveyA, "u3");
        var other = await harness.StartAsync(surveyB, "u2");

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(2);
        await harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(batchSize: 1, cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

        // Assert: the accommodated responses keep going, and survey B's response is submitted in the same run.
        Assert.Equal(SurveyResponseStatus.InProgress, (await harness.FindAsync(accommodated.Response.ItemId)).Status);
        Assert.Equal(SurveyResponseStatus.InProgress, (await harness.FindAsync(byAudience.Response.ItemId)).Status);
        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(other.Response.ItemId)).Status);
    }

    [Fact]
    public async Task Upkeep_OnAnUpgradedSite_LeavesTheResponsesOfASurveyClosedBeforeTheUpgradeAlone_AndSubmitsTheOthers()
    {
        // Arrange: the survey "before" closed before the close step started on this site (the upgrade); "after" closes
        // after it, and "manual" was closed by staff after it.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var before = await harness.PublishAsync(OnePage(), item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(1));
        var after = await harness.PublishAsync(OnePage(), item => item.Access.ClosesUtc = RuntimeTestHarness.Now.AddHours(3));
        var manual = await harness.PublishAsync(OnePage());
        var old = await harness.StartAsync(before, "u1");
        var current = await harness.StartAsync(after, "u1");
        var closedByStaff = await harness.StartAsync(manual, "u1");

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(2);
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<global::YesSql.ISession>();
            await session.SaveAsync(new SurveyCloseStepStart { StartedUtc = harness.Clock.UtcNow }, false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            return true;
        });
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var survey = await store.FindByIdAsync(manual.ItemId, TestContext.Current.CancellationToken);
            survey.Status = SurveyStatus.Closed;
            survey.ClosedUtc = harness.Clock.UtcNow;
            await store.UpdateAsync(survey, TestContext.Current.CancellationToken);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

            return true;
        });

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(4);
        await harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        Assert.Equal(SurveyResponseStatus.InProgress, (await harness.FindAsync(old.Response.ItemId)).Status);
        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(current.Response.ItemId)).Status);
        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(closedByStaff.Response.ItemId)).Status);
    }

    private static SurveyDefinition OnePage()
        => RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));

    private sealed class FakeOverrides : ISurveyOverrideService
    {
        public List<SurveyOverride> Items { get; } = [];

        public HashSet<string> AudienceMembers { get; } = new(StringComparer.Ordinal);

        public Task<SurveyErrorCode> GrantAsync(SurveyOverride surveyOverride, CancellationToken cancellationToken = default)
            => Task.FromResult(SurveyErrorCode.None);

        public Task<IReadOnlyList<SurveyOverride>> ListAsync(string surveyId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SurveyOverride>>(Items.Where(item => item.SurveyId == surveyId).ToList());

        public Task<bool> RevokeAsync(string overrideId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<SurveyOverride> FindEffectiveAsync(string surveyId, string userId, CrestApps.OrchardCore.Records.RecordRef? context, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(item => item.SurveyId == surveyId
                && (item.UserId == userId || (!string.IsNullOrEmpty(item.AudienceId) && userId is not null && AudienceMembers.Contains(userId)))));
    }
}
