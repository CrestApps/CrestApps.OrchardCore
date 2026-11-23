using System.Diagnostics;
using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Core.Services;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Invitations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Outreach;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Settings;
using YesSql;
using OutreachRecordingSender = CrestApps.OrchardCore.Tests.Outreach.RecordingSender;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

/// <summary>
/// Delivery of survey invitations: the daily e-mail limit (R3-M6, R4-L3, exact across servers after round 4; one batch
/// per scope, one wait for a busy lock and a bounded sending window after R5-M3), held invitations (R4-L4) and an
/// unreadable opt-out list (R3-M4, R4-M3).
/// </summary>
public sealed class SurveyInvitationDeliveryTests
{
    // The daily e-mail limit's lock of 19 November in the site's time zone.
    private const string DayLockKey = "Surveys:InvitationEmails:2026-11-19";

    // 18:00 on 19 November in New York; the site's day there ends at 05:00 UTC on 20 November.
    private static readonly DateTime _evening = new(2026, 11, 19, 23, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OverTheDailyLimit_AnInvitationIsHeld_AndTheReminderTaskSendsItOnTheSitesNextDay()
    {
        // Arrange: one e-mail a day, counted in the site's time zone.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act: the first goes out, the second is held, and a third is only created, never sent.
        var first = await InviteAsync(harness, survey, "first@example.com");
        var held = await InviteAsync(harness, survey, "held@example.com");
        var created = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "created@example.com" }));
        var collectorId = first.Invitation.CollectorId;

        // A new UTC day, but still the same day in New York: nothing more goes out.
        harness.Clock.UtcNow = _evening.AddHours(2);
        var sameSiteDay = await RemindAsync(harness, collectorId);

        // Just after midnight in New York: the held invitation goes out; the created one stays as it is.
        harness.Clock.UtcNow = _evening.AddHours(6.5);
        var nextSiteDay = await RemindAsync(harness, collectorId);

        // Assert
        Assert.Equal(0, sameSiteDay);
        Assert.Equal(1, nextSiteDay);
        Assert.Equal([first.Link, held.Link], sender.Sent);

        var heldNow = await FindAsync(harness, held.Invitation.ItemId);
        var createdNow = await FindAsync(harness, created.Invitation.ItemId);
        Assert.Equal(SurveyInvitationStatus.Sent, heldNow.Status);
        Assert.False(heldNow.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        Assert.Equal(SurveyInvitationStatus.Pending, createdNow.Status);
    }

    [Fact]
    public async Task TheDailyLimit_CountsEmails_NotInvitations()
    {
        // Arrange: two e-mails a day.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 2);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act: one invitation and its manual reminder are two e-mails, so the next invitation waits.
        var first = await InviteAsync(harness, survey, "first@example.com");
        harness.Clock.UtcNow = _evening.AddMinutes(10);
        await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().RemindAsync(first.Invitation.CollectorId, force: true));
        var second = await InviteAsync(harness, survey, "second@example.com");

        // Assert
        Assert.Equal(2, sender.Sent.Count);
        Assert.Equal(SurveyInvitationStatus.Pending, (await FindAsync(harness, second.Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task AnUnreadableOptOutList_HoldsInvitationsUnchanged_AndTheConsentServiceAnswersUnknownInsteadOfOptingEveryoneOut()
    {
        // Arrange: an address unsubscribed, then the list's Data Protection key is lost.
        var sender = new RecordingSender();
        var protection = new BreakableDataProtection();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100, protection);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<IConsentService>().RecordAsync(ConsentChannels.Email, "gone@example.com", new ConsentChange
            {
                OptedOut = true,
                Source = SurveyInvitationConsentSource.SourceName,
            });

            return true;
        });

        protection.Broken = true;

        // Act
        var stays = await InviteAsync(harness, survey, "stays@example.com");
        var gone = await InviteAsync(harness, survey, "gone@example.com");
        var consent = await harness.RunAsync(services => services.GetRequiredService<IConsentService>().GetAsync(ConsentChannels.Email, "anyone@example.com"));
        var outreachSkip = await harness.RunAsync(services => services.GetServices<IOutreachRecipientPolicy>().OfType<ConsentOutreachRecipientPolicy>().Single().GetSkipReasonAsync(
            new OutreachRun { ConsentChannel = ConsentChannels.Email },
            new OutreachRecipient { Key = "r-1", Address = "anyone@example.com" }));
        var whileBroken = (Stays: await FindAsync(harness, stays.Invitation.ItemId), Gone: await FindAsync(harness, gone.Invitation.ItemId));

        // The keys come back: the next reminder run sends the held invitation and only now marks the unsubscribed one.
        protection.Broken = false;
        await RemindAsync(harness, stays.Invitation.CollectorId);

        // Assert
        Assert.False(consent.HasOptedOut);
        Assert.True(consent.IsUnknown);
        Assert.Equal([SurveyInvitationConsentSource.SourceName], consent.UnknownSources);
        Assert.Null(outreachSkip);
        Assert.Equal(SurveyInvitationStatus.Pending, whileBroken.Stays.Status);
        Assert.Equal(SurveyInvitationStatus.Pending, whileBroken.Gone.Status);
        Assert.True(whileBroken.Stays.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        Assert.Equal([stays.Link], sender.Sent);
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, stays.Invitation.ItemId)).Status);
        Assert.Equal(SurveyInvitationStatus.OptedOut, (await FindAsync(harness, gone.Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task AnUnreadableOptOutList_DoesNotFailAnOutreachRun_AndTheOtherConsentSourcesStillDecide()
    {
        // Arrange: an Outreach e-mail run on a site with the survey invitations consent source and one other source.
        var protection = new BreakableDataProtection();
        var audience = new ListAudienceSource(
            new OutreachRecipient { Key = "r-1", Address = "news@example.com" },
            new OutreachRecipient { Key = "r-2", Address = "stop@example.com" });
        var outreachSender = new OutreachRecordingSender();
        var other = new Mock<IConsentSource>();
        other.SetupGet(source => source.Name).Returns("other");
        other.Setup(source => source.GetAnswerAsync(It.IsAny<ConsentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsentQuery query, CancellationToken _) => query.Address == "stop@example.com" ? ConsentAnswer.OptedOut : ConsentAnswer.NotOptedOut);

        await using var harness = await OutreachTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<IOutreachAudienceSource>(audience);
            services.AddSingleton<IOutreachChannelSender>(outreachSender);
            services.AddSingleton<IDataProtectionProvider>(protection);
            services.AddScoped<SurveyInvitationOptOutStore>();
            services.AddConsentSource<SurveyInvitationConsentSource>();
            services.AddSingleton(other.Object);
        });

        // An address unsubscribed from survey invitations, then the list's Data Protection key is lost.
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IConsentService>().RecordAsync(ConsentChannels.Email, "gone@example.com", new ConsentChange
            {
                OptedOut = true,
                Source = SurveyInvitationConsentSource.SourceName,
            }, TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        protection.Broken = true;

        var run = await harness.StartAsync(new OutreachRun
        {
            Name = "Newsletter",
            OwnerType = "Test",
            OwnerId = "owner-1",
            AudienceSource = audience.Name,
            Sender = outreachSender.Name,
            ConsentChannel = ConsentChannels.Email,
            Template = new OutreachTemplate { Body = "Hello" },
        });

        // Act
        await harness.ProcessDueAsync();

        // Assert: the run completes; the address the other source opted out is skipped, the rest is sent.
        var stored = await harness.GetRunAsync(run.ItemId);

        Assert.Equal(OutreachRunStatus.Completed, stored.Status);
        Assert.Equal((1, 1, 0), (stored.Sent, stored.Skipped, stored.Failed));
        Assert.Equal(["news@example.com"], outreachSender.Sent.Select(send => send.Address));
    }

    [Fact]
    public async Task OneSendOfManyInvitations_ReadsTheLimitOnce_AndCountsItsOwnSends()
    {
        // Arrange: three e-mails a day and ten invitations created but not sent.
        var sender = new RecordingSender();
        var siteService = SiteService(emailsPerDay: 3);
        await using var harness = await CreateAsync(sender, siteService);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        string collectorId = null;

        for (var i = 0; i < 10; i++)
        {
            var created = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = $"guest{i}@example.com" }));
            collectorId = created.Invitation.CollectorId;
        }

        siteService.Invocations.Clear();

        // Act
        var sent = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().SendPendingAsync(collectorId));

        // Assert: three go out in the one call. The site settings are read exactly twice for the whole send: once for the
        // links' base address and once for the limit (its limit and time zone), not once more for any invitation.
        Assert.Equal(3, sent);
        Assert.Equal(3, sender.Sent.Count);
        Assert.Equal(2, siteService.Invocations.Count(invocation => invocation.Method.Name == nameof(ISiteService.GetSiteSettingsAsync)));
    }

    [Fact]
    public async Task AHeldInvitation_IsSent_BehindMoreNeverSentInvitationsThanOneRead()
    {
        // Arrange: one e-mail a day; the first goes out.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var first = await InviteAsync(harness, survey, "first@example.com");
        var collectorId = first.Invitation.CollectorId;

        // More invitations created and never sent than one read of the collector returns, ahead of the held one.
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();

            for (var i = 0; i < CrestApps.OrchardCore.Surveys.SurveysConstants.PageSize + 10; i++)
            {
                await store.CreateAsync(new SurveyInvitation
                {
                    ItemId = "created-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
                    SurveyId = survey.ItemId,
                    CollectorId = collectorId,
                    Email = $"created{i}@example.com",
                    Status = SurveyInvitationStatus.Pending,
                    CreatedUtc = _evening,
                });
            }

            return true;
        });

        var held = await InviteAsync(harness, survey, "held@example.com");

        // Act: the site's next day.
        harness.Clock.UtcNow = _evening.AddHours(6.5);
        var sent = await RemindAsync(harness, collectorId);

        // Assert
        Assert.Equal(1, sent);
        Assert.Equal([first.Link, held.Link], sender.Sent);
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, held.Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task TwoSendersAtOnceNearTheLimit_NeverPassIt()
    {
        // Arrange: three e-mails a day, one already sent today, and two senders (two servers, or two requests) that each
        // want to send three more at the same moment.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 3);
        harness.Clock.UtcNow = _evening;
        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(SentInvitation("earlier@example.com"));

            return true;
        });

        using var bothStarted = new Barrier(2);

        // Each sender asks the limit before every e-mail, takes its time sending it, and saves its e-mails when its scope
        // ends, as a send does.
        Task<int> SendAsync(string name) => Task.Run(() => harness.RunAsync(async services =>
        {
            var limit = services.GetRequiredService<SurveyInvitationEmailLimit>();
            var store = services.GetRequiredService<SurveyInvitationStore>();
            var sent = 0;

            bothStarted.SignalAndWait(TestContext.Current.CancellationToken);

            for (var i = 0; i < 3; i++)
            {
                if (!await limit.CanSendAsync(TestContext.Current.CancellationToken))
                {
                    break;
                }

                await Task.Delay(25, TestContext.Current.CancellationToken);
                await store.CreateAsync(SentInvitation($"{name}{i}@example.com"));
                limit.AddSent();
                sent++;
            }

            return sent;
        }));

        // Act
        var results = await Task.WhenAll(SendAsync("one"), SendAsync("two"));

        // Assert: two more went out between them, and the day's count is exactly the limit.
        var emailed = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().CountEmailedSinceAsync(SurveyInvitationService.DayStartUtc(_evening, "America/New_York")));

        Assert.Equal(2, results.Sum());
        Assert.Equal(3, emailed);
    }

    [Fact]
    public async Task WhenTheDailyLimitsLockIsBusy_InvitationsAreHeld_NotSent_AndTheBatchWaitsForItOnce()
    {
        // Arrange: another sender holds the day's lock past the wait, and two invitations were created, not sent.
        var sender = new RecordingSender();
        var busyLock = new BusyDistributedLock();
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 10), configure: services => services.AddSingleton<IDistributedLock>(busyLock));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var first = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "one@example.com" }));
        var second = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "two@example.com" }));

        // Act
        var sent = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().SendPendingAsync(first.Invitation.CollectorId));

        // Assert: nothing went out, the lock was asked for once for the whole batch, and both invitations wait, held,
        // for the reminder task's next run.
        var held = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListHeldByCollectorAsync(first.Invitation.CollectorId, 10));

        Assert.Equal(0, sent);
        Assert.Empty(sender.Sent);
        Assert.Equal(1, busyLock.Attempts);
        Assert.Equal(new[] { first.Invitation.ItemId, second.Invitation.ItemId }.Order(StringComparer.Ordinal), held.Select(item => item.ItemId).Order(StringComparer.Ordinal));
        Assert.All(held, invitation => Assert.Equal(SurveyInvitationStatus.Pending, invitation.Status));
    }

    [Fact]
    public async Task TheDailyLimitsLock_IsReleasedWhenTheSendingScopeEnds()
    {
        // Arrange
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 10);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act: a send in a shell scope (released after its save) and one in a plain service scope (released when the
        // scope is disposed).
        await harness.RunInShellAsync(services => services.GetRequiredService<ISurveyInvitationService>().InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "shell@example.com" }));
        await InviteAsync(harness, survey, "plain@example.com");

        // Assert
        var locks = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IDistributedLock>()));

        Assert.Equal(2, sender.Sent.Count);
        Assert.False(await locks.IsLockAcquiredAsync("Surveys:InvitationEmails:2026-11-19"));
    }

    [Fact]
    public async Task ABulkInvite_WhileAnotherSenderHoldsTheLock_WaitsOnce_HoldsTheRest_AndFinishesQuickly()
    {
        // Arrange: another sender holds the day's lock for the whole invite; each wait for it lasts 400 ms here (10 s on
        // a site), so a wait per recipient would take 30 x 400 ms = 12 s.
        var sender = new RecordingSender();
        var locks = new InvitationEmailLockDouble(TimeSpan.FromMilliseconds(400));
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services => services.AddSingleton<IDistributedLock>(locks));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var requests = Enumerable.Range(0, 30).Select(i => new SurveyInvitationRequest { Email = $"guest{i}@example.com" }).ToList();
        var otherSender = await locks.AcquireLockAsync(DayLockKey);
        var stopwatch = Stopwatch.StartNew();

        // Act: the Invite page's Send now.
        var results = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().InviteManyAsync(survey.ItemId, null, requests, send: true));
        stopwatch.Stop();
        var attemptsDuringInvite = locks.Attempts;
        var collectorId = results[0].Invitation.CollectorId;
        var held = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListHeldByCollectorAsync(collectorId, 100));

        // The other sender saves and lets go; the next reminder run sends the held invitations.
        await otherSender.DisposeAsync();
        var later = await RemindAsync(harness, collectorId);

        // Assert: one wait for the whole invite, nothing sent, all 30 created and held, well under the time of two waits
        // per recipient; then the reminder run sends them all.
        Assert.Equal(30, results.Count(result => result.Succeeded));
        Assert.Equal(30, held.Count);
        Assert.All(held, invitation => Assert.Equal(SurveyInvitationStatus.Pending, invitation.Status));
        Assert.Equal(1, attemptsDuringInvite);
        Assert.True(stopwatch.Elapsed < locks.Wait * 10, $"The invite took {stopwatch.Elapsed}.");
        Assert.Equal(30, later);
        Assert.Equal(30, sender.Sent.Count);
    }

    [Fact]
    public async Task ManyInvitesInOneScope_AsAWorkflowLoopRunsThem_AskForTheBusyLockOnce()
    {
        // Arrange: another sender holds the day's lock.
        var sender = new RecordingSender();
        var locks = new InvitationEmailLockDouble(TimeSpan.FromMilliseconds(200));
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services => services.AddSingleton<IDistributedLock>(locks));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        await using var otherSender = await locks.AcquireLockAsync(DayLockKey);

        // Act: ten single invitations in one scope, as a workflow's loop of Send Survey Invitation tasks sends them.
        var results = await harness.RunAsync(async services =>
        {
            var invitations = services.GetRequiredService<ISurveyInvitationService>();
            var list = new List<SurveyInvitationResult>();

            for (var i = 0; i < 10; i++)
            {
                list.Add(await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = $"loop{i}@example.com" }));
            }

            return list;
        });

        // Assert
        Assert.Equal(1, locks.Attempts);
        Assert.Empty(sender.Sent);
        Assert.All(results, result => Assert.True(result.Invitation.TryGet<SurveyInvitationHold>(out var hold) && hold is not null));
    }

    [Fact]
    public async Task ASend_StopsAfterItsSendingWindow_AndHoldsTheRest_WellBeforeTheLockExpires()
    {
        // Arrange: ten invitations created, and a sender that takes two minutes for each e-mail.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        sender.OnSend = () => harness.Clock.UtcNow = harness.Clock.UtcNow.AddMinutes(2);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var requests = Enumerable.Range(0, 10).Select(i => new SurveyInvitationRequest { Email = $"slow{i}@example.com" }).ToList();

        // Act
        var results = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().InviteManyAsync(survey.ItemId, null, requests, send: true));
        var held = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListHeldByCollectorAsync(results[0].Invitation.CollectorId, 100));

        // Assert: e-mails go out only during the five minutes after the lock was taken (at 0, 2 and 4 minutes); the rest
        // are held for the reminder task. The lock lasts far longer than the window, so the send's save comes first.
        Assert.Equal(3, sender.Sent.Count);
        Assert.Equal(7, held.Count);
        Assert.Equal(TimeSpan.FromMinutes(5), SurveyInvitationEmailLimit.SendingWindow);
        Assert.True(SurveyInvitationEmailLimit.LockExpiration >= SurveyInvitationEmailLimit.SendingWindow * 5);
    }

    [Fact]
    public async Task InAShellScope_TheSessionIsSavedBeforeTheLockIsReleased_SoTheNextSenderCountsTheEmails()
    {
        // Arrange: three e-mails a day; the session is saved as Orchard Core saves it, when the shell scope ends, and the
        // save is slow, so a lock released before the save would let the next sender count too few e-mails.
        var sender = new RecordingSender();
        var locks = new KeyedDistributedLock();
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 3), configure: services =>
        {
            services.AddSingleton<IDistributedLock>(locks);
            RuntimeTestHarness.UseOrchardSession(services, () => Task.Delay(300));
        });
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        using var secondAsking = new SemaphoreSlim(0);
        Task<(int CountedFirst, IReadOnlyList<SurveyInvitationResult> Results)> second = null;

        // Act: the first sender sends two e-mails in a shell scope, as a request does, and ends without saving itself.
        // While it still holds the lock, a second sender in its own shell scope asks for the lock, then invites two more.
        await harness.RunInShellScopeAsync(async services =>
        {
            await services.GetRequiredService<SurveyInvitationService>().InviteManyAsync(survey.ItemId, null,
            [
                new SurveyInvitationRequest { Email = "a1@example.com" },
                new SurveyInvitationRequest { Email = "a2@example.com" },
            ], send: true);

            using (ExecutionContext.SuppressFlow())
            {
                second = Task.Run(() => harness.RunInShellScopeAsync(async secondServices =>
                {
                    // The second sender asks for the lock before it reads anything, so the test's SQLite file isn't
                    // read-locked while the first sender saves.
                    var limit = secondServices.GetRequiredService<SurveyInvitationEmailLimit>();

                    secondAsking.Release();
                    await limit.CanSendAsync(TestContext.Current.CancellationToken);

                    var counted = await secondServices.GetRequiredService<SurveyInvitationStore>().CountEmailedSinceAsync(SurveyInvitationService.DayStartUtc(_evening, "America/New_York"));
                    var results = await secondServices.GetRequiredService<SurveyInvitationService>().InviteManyAsync(survey.ItemId, null,
                    [
                        new SurveyInvitationRequest { Email = "b1@example.com" },
                        new SurveyInvitationRequest { Email = "b2@example.com" },
                    ], send: true);

                    return (counted, results);
                }));
            }

            await secondAsking.WaitAsync(TestContext.Current.CancellationToken);
            await Task.Delay(100, TestContext.Current.CancellationToken);

            Assert.False(second.IsCompleted);
            Assert.True(await locks.IsLockAcquiredAsync(DayLockKey));

            return true;
        });

        var (countedFirst, secondResults) = await second;

        // Assert: the second sender got the lock only after the first scope's save, counted its two e-mails, sent one
        // more and held the other, so the day's e-mails are exactly the limit.
        var emailed = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().CountEmailedSinceAsync(SurveyInvitationService.DayStartUtc(_evening, "America/New_York")));

        Assert.Equal(2, countedFirst);
        Assert.Equal(3, sender.Sent.Count);
        Assert.Equal(3, emailed);
        Assert.Equal(SurveyInvitationStatus.Sent, secondResults[0].Invitation.Status);
        Assert.True(secondResults[1].Invitation.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        Assert.False(await locks.IsLockAcquiredAsync(DayLockKey));
    }

    [Fact]
    public async Task TwoFirstUnsubscribesAtOnce_EachKeepTheirAddress()
    {
        // Arrange: two sessions both find no list and each insert one.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (store, protection) = await harness.RunAsync(services => Task.FromResult((services.GetRequiredService<IStore>(), services.GetRequiredService<IDataProtectionProvider>())));

        await using (var one = store.CreateSession())
        await using (var two = store.CreateSession())
        {
            var first = new SurveyInvitationOptOutStore(one, protection, NullLogger<SurveyInvitationOptOutStore>.Instance);
            var second = new SurveyInvitationOptOutStore(two, protection, NullLogger<SurveyInvitationOptOutStore>.Instance);

            Assert.Equal(SurveyInvitationOptOutState.NotOptedOut, await first.GetStateAsync("a@example.com", TestContext.Current.CancellationToken));
            Assert.Equal(SurveyInvitationOptOutState.NotOptedOut, await second.GetStateAsync("b@example.com", TestContext.Current.CancellationToken));

            await first.SetAsync("a@example.com", optedOut: true, TestContext.Current.CancellationToken);
            await second.SetAsync("b@example.com", optedOut: true, TestContext.Current.CancellationToken);
            await one.SaveChangesAsync(TestContext.Current.CancellationToken);
            await two.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using var read = store.CreateSession();
        var reader = new SurveyInvitationOptOutStore(read, protection, NullLogger<SurveyInvitationOptOutStore>.Instance);
        var a = await reader.GetStateAsync("a@example.com", TestContext.Current.CancellationToken);
        var b = await reader.GetStateAsync("b@example.com", TestContext.Current.CancellationToken);
        await reader.SetAsync("b@example.com", optedOut: false, TestContext.Current.CancellationToken);
        await read.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var again = store.CreateSession();
        var bAfterOptIn = await new SurveyInvitationOptOutStore(again, protection, NullLogger<SurveyInvitationOptOutStore>.Instance).GetStateAsync("b@example.com", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, (await again.Query<SurveyInvitationOptOutDocument>().ListAsync(TestContext.Current.CancellationToken)).Count);
        Assert.Equal(SurveyInvitationOptOutState.OptedOut, a);
        Assert.Equal(SurveyInvitationOptOutState.OptedOut, b);
        Assert.Equal(SurveyInvitationOptOutState.NotOptedOut, bAfterOptIn);
    }

    [Theory]
    [InlineData("2026-11-19T23:00:00Z", "America/New_York", "2026-11-19T05:00:00Z")]
    [InlineData("2026-11-20T05:30:00Z", "America/New_York", "2026-11-20T05:00:00Z")]
    [InlineData("2026-11-19T23:00:00Z", "", "2026-11-19T00:00:00Z")]
    public void DayStartUtc_IsMidnightInTheSitesTimeZone(string utcNow, string timeZoneId, string expected)
        => Assert.Equal(
            DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
            SurveyInvitationService.DayStartUtc(DateTime.Parse(utcNow, null, System.Globalization.DateTimeStyles.AdjustToUniversal), timeZoneId));

    private static Task<RuntimeTestHarness> CreateAsync(RecordingSender sender, int emailsPerDay, BreakableDataProtection protection = null)
        => CreateAsync(sender, SiteService(emailsPerDay), protection);

    private static Mock<ISiteService> SiteService(int emailsPerDay)
    {
        var site = new SiteSettings { TimeZoneId = "America/New_York" };
        site.Put(new SurveySettings { InvitationEmailsPerDay = emailsPerDay });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        return siteService;
    }

    private static async Task<RuntimeTestHarness> CreateAsync(RecordingSender sender, Mock<ISiteService> siteService, BreakableDataProtection protection = null, Action<IServiceCollection> configure = null)
    {
        return await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSurveysInvitations();
            services.AddSingleton<ISurveyInvitationSender>(sender);
            services.AddSurveyInvitationRecipientPolicy<OutreachSurveyInvitationRecipientPolicy>();
            services.AddOutreachRecipientPolicy<ConsentOutreachRecipientPolicy>();
            services.AddScoped<SurveyInvitationOptOutStore>();
            services.AddConsentSource<SurveyInvitationConsentSource>();
            services.AddSingleton(siteService.Object);

            if (protection is not null)
            {
                services.AddSingleton<IDataProtectionProvider>(protection);
            }

            configure?.Invoke(services);
        });
    }

    private static SurveyInvitation SentInvitation(string email)
        => new()
        {
            ItemId = IdGenerator.GenerateId(),
            SurveyId = "s1",
            CollectorId = "c1",
            Email = email,
            Status = SurveyInvitationStatus.Sent,
            CreatedUtc = _evening,
            SentUtc = _evening,
        };

    private static Task<SurveyInvitationResult> InviteAsync(RuntimeTestHarness harness, Survey survey, string email)
        => harness.RunAsync(services => services.GetRequiredService<ISurveyInvitationService>().InviteAsync(new SurveyInvitationRequest
        {
            SurveyId = survey.ItemId,
            Email = email,
        }));

    private static Task<int> RemindAsync(RuntimeTestHarness harness, string collectorId)
        => harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().RemindAsync(collectorId, force: false));

    private static Task<SurveyInvitation> FindAsync(RuntimeTestHarness harness, string invitationId)
        => harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().FindByIdAsync(invitationId).AsTask());

    private sealed class RecordingSender : ISurveyInvitationSender
    {
        public List<string> Sent { get; } = [];

        public Action OnSend { get; set; }

        public Task<bool> SendAsync(Survey survey, SurveyCollector collector, SurveyInvitation invitation, string link, bool isReminder, CancellationToken cancellationToken = default)
        {
            lock (Sent)
            {
                Sent.Add(link);
            }

            OnSend?.Invoke();

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Locks whose daily e-mail limit lock another sender always holds: every attempt to take it gives up. Other keys
    /// (the opt-out list's, for example) behave as usual.
    /// </summary>
    private sealed class BusyDistributedLock : IDistributedLock
    {
        private const string BusyPrefix = "Surveys:InvitationEmails:";

        private readonly KeyedDistributedLock _others = new();
        private int _attempts;

        public int Attempts => _attempts;

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => key.StartsWith(BusyPrefix, StringComparison.Ordinal) ? throw new InvalidOperationException("The lock is busy.") : _others.AcquireLockAsync(key, expiration);

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            if (!key.StartsWith(BusyPrefix, StringComparison.Ordinal))
            {
                return _others.TryAcquireLockAsync(key, timeout, expiration);
            }

            Interlocked.Increment(ref _attempts);

            return Task.FromResult<(ILocker, bool)>((null, false));
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => key.StartsWith(BusyPrefix, StringComparison.Ordinal) ? Task.FromResult(true) : _others.IsLockAcquiredAsync(key);
    }

    /// <summary>
    /// Data Protection whose opt-out list protector can lose its key, as when the keys are missing on a node.
    /// </summary>
    private sealed class BreakableDataProtection : IDataProtectionProvider
    {
        private readonly EphemeralDataProtectionProvider _inner = new();

        public bool Broken { get; set; }

        public IDataProtector CreateProtector(string purpose)
        {
            var protector = _inner.CreateProtector(purpose);

            return string.Equals(purpose, SurveyInvitationOptOutStore.Purpose, StringComparison.Ordinal) ? new Protector(protector, this) : protector;
        }

        private sealed class Protector : IDataProtector
        {
            private readonly IDataProtector _inner;
            private readonly BreakableDataProtection _owner;

            public Protector(IDataProtector inner, BreakableDataProtection owner)
            {
                _inner = inner;
                _owner = owner;
            }

            public IDataProtector CreateProtector(string purpose)
                => _inner.CreateProtector(purpose);

            public byte[] Protect(byte[] plaintext)
                => _inner.Protect(plaintext);

            public byte[] Unprotect(byte[] protectedData)
                => _owner.Broken ? throw new CryptographicException("The key ring has no key for this payload.") : _inner.Unprotect(protectedData);
        }
    }
}
