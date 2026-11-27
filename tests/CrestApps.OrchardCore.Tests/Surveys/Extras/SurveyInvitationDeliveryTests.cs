using System.Diagnostics;
using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Core.Services;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
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
    // The tenant's invitation e-mail lock (one per tenant, not one per day: R7-Mi9).
    private const string DayLockKey = SurveyInvitationEmailLimit.LockKey;

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
        await AddOptOutTableAsync(harness.Store);

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
    public async Task ASendWhoseLockWaitTimesOut_ReadsAndWritesNothing_AndSaysAnotherSendIsGoingOut()
    {
        // Arrange: another sender holds the e-mail lock past the wait, and two invitations were created, not sent
        // (R7-Mi6).
        var sender = new RecordingSender();
        var busyLock = new BusyDistributedLock();
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 10), configure: services => services.AddSingleton<IDistributedLock>(busyLock));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var first = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "one@example.com" }));
        var second = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "two@example.com" }));

        // Act
        var (sent, busy) = await harness.RunAsync(async services =>
        {
            var invitations = services.GetRequiredService<SurveyInvitationService>();
            var count = await invitations.SendPendingAsync(first.Invitation.CollectorId);

            return (count, invitations.AnotherSendIsGoingOut);
        });

        // Assert: nothing went out, the lock was asked for once, the send says another send is going out, and neither
        // invitation was written (no hold mark on a copy read while the other send was still saving): Send works again
        // once the other send is done.
        var all = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListByCollectorAsync(first.Invitation.CollectorId, null, 10));

        Assert.Equal(0, sent);
        Assert.True(busy);
        Assert.Empty(sender.Sent);
        Assert.Equal(1, busyLock.Attempts);
        Assert.Equal(new[] { first.Invitation.ItemId, second.Invitation.ItemId }.Order(StringComparer.Ordinal), all.Select(item => item.ItemId).Order(StringComparer.Ordinal));
        Assert.All(all, invitation =>
        {
            Assert.Equal(SurveyInvitationStatus.Pending, invitation.Status);
            Assert.False(invitation.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        });
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
        Assert.False(await locks.IsLockAcquiredAsync(DayLockKey));
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

        // Act: the first sender sends two e-mails in a shell scope, as a request does; since R8-M3 the send commits what it
        // sent and only then lets go of the lock itself. A second sender in its own shell scope asks for the lock, then
        // invites two more.
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

            return true;
        });

        var (countedFirst, secondResults) = await second;

        // Assert: the second sender got the lock only after the first send's commit, counted its two e-mails, sent one
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
    public async Task Reminders_ReachADueInvitation_BehindMoreUsedUpOnesThanOneRead_AndExpireTheOnesNotDue()
    {
        // Arrange: a list with one reminder a day; more invitations that used up their reminders than one read returns,
        // one of them expired, then an invitation whose reminder is due.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1000);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var first = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "created@example.com" }));
        var collectorId = first.Invitation.CollectorId;
        var expiredId = IdGenerator.GenerateId();

        await harness.RunAsync(async services =>
        {
            var collectors = services.GetRequiredService<SurveyCollectorStore>();
            var collector = await collectors.FindByIdAsync(collectorId);
            collector.Put(new SurveyInvitationSettings { ReminderCount = 1, ReminderEveryDays = 1 });
            await collectors.UpdateAsync(collector);

            var store = services.GetRequiredService<SurveyInvitationStore>();

            for (var i = 0; i < CrestApps.OrchardCore.Surveys.SurveysConstants.PageSize + 10; i++)
            {
                await store.CreateAsync(new SurveyInvitation
                {
                    ItemId = i == 5 ? expiredId : IdGenerator.GenerateId(),
                    SurveyId = survey.ItemId,
                    CollectorId = collectorId,
                    Email = $"used{i}@example.com",
                    Status = SurveyInvitationStatus.Sent,
                    CreatedUtc = _evening,
                    SentUtc = _evening.AddDays(-3),
                    RemindersSent = 1,
                    LastReminderUtc = _evening.AddDays(-2),
                    ExpiresUtc = i == 5 ? _evening.AddHours(1) : null,
                });
            }

            return true;
        });

        var due = await InviteAsync(harness, survey, "due@example.com");

        // Act: two days later.
        harness.Clock.UtcNow = _evening.AddDays(2);
        var sent = await RemindAsync(harness, collectorId);

        // Assert
        Assert.Equal(1, sent);
        Assert.Equal(2, sender.Sent.Count);
        Assert.Equal(1, (await FindAsync(harness, due.Invitation.ItemId)).RemindersSent);
        Assert.Equal(SurveyInvitationStatus.Expired, (await FindAsync(harness, expiredId)).Status);
    }

    [Fact]
    public async Task AReminderThatKeepsFailing_StopsAfterItsAttempts_TheInvitationStaysOpen_AndInvitingAgainStartsThemAgain()
    {
        // Arrange: a delivered invitation on a list with three reminders a day apart; then the mail server refuses the
        // address every time, while it takes the other e-mails of each run.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1000);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var invited = await InviteAsync(harness, survey, "refused@example.com");
        await SetRemindersAsync(harness, invited.Invitation.CollectorId, count: 3, everyDays: 1);
        sender.Outcome = invitation => invitation.Email == "refused@example.com" ? SurveyInvitationDelivery.RetryLater : SurveyInvitationDelivery.Delivered;
        var refusedAttempts = 0;
        sender.OnAttempt = invitation => refusedAttempts += invitation.Email == "refused@example.com" ? 1 : 0;

        // Act: two days later, eight hourly runs; each run's send e-mails someone else first.
        for (var run = 0; run < 8; run++)
        {
            harness.Clock.UtcNow = _evening.AddDays(2).AddHours(run);
            await InviteThenRemindAsync(harness, survey, $"other{run}@example.com", invited.Invitation.CollectorId);
        }

        var stopped = await FindAsync(harness, invited.Invitation.ItemId);
        sender.Outcome = null;
        await InviteAsync(harness, survey, "refused@example.com");
        var invitedAgain = await FindAsync(harness, invited.Invitation.ItemId);

        // Assert: tried MaxSendAttempts times, then no more; the invitation is still open, and inviting again clears it.
        Assert.Equal(SurveyInvitationHold.MaxSendAttempts, refusedAttempts);
        Assert.Equal(SurveyInvitationStatus.Sent, stopped.Status);
        Assert.True(SurveyInvitationReminderFailures.HaveStopped(stopped));
        Assert.Equal(0, stopped.RemindersSent);
        Assert.False(SurveyInvitationReminderFailures.HaveStopped(invitedAgain));
    }

    [Fact]
    public async Task AReminderThatCanNeverBeDelivered_StopsTheReminders_AtOnce()
    {
        // Arrange
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1000);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var invited = await InviteAsync(harness, survey, "gone@example.com");
        await SetRemindersAsync(harness, invited.Invitation.CollectorId, count: 3, everyDays: 1);
        sender.Outcome = _ => SurveyInvitationDelivery.Undeliverable;
        var attemptsBefore = sender.Attempts;

        // Act
        for (var run = 0; run < 3; run++)
        {
            harness.Clock.UtcNow = _evening.AddDays(2).AddHours(run);
            await RemindAsync(harness, invited.Invitation.CollectorId);
        }

        // Assert
        Assert.Equal(1, sender.Attempts - attemptsBefore);
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, invited.Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task VisitDueForReminder_ReadsOnlyTheInvitationsTheIndexSaysAreDue()
    {
        // Arrange: one due invitation among ones sent too recently, ones that used up their reminders, one whose
        // reminders stopped, and one of another list.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1000);
        var dueId = IdGenerator.GenerateId();
        var dueAfterReminderId = IdGenerator.GenerateId();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();

            SurveyInvitation Make(string id, string collector, DateTime? sent, int reminders, DateTime? lastReminder)
                => new()
                {
                    ItemId = id ?? IdGenerator.GenerateId(),
                    SurveyId = "s1",
                    CollectorId = collector,
                    Email = IdGenerator.GenerateId() + "@example.com",
                    Status = SurveyInvitationStatus.Sent,
                    CreatedUtc = _evening.AddDays(-10),
                    SentUtc = sent,
                    RemindersSent = reminders,
                    LastReminderUtc = lastReminder,
                };

            await store.CreateAsync(Make(dueId, "c1", _evening.AddDays(-3), 0, null));
            await store.CreateAsync(Make(dueAfterReminderId, "c1", _evening.AddDays(-9), 1, _evening.AddDays(-3)));
            await store.CreateAsync(Make(null, "c1", _evening.AddHours(-1), 0, null));
            await store.CreateAsync(Make(null, "c1", _evening.AddDays(-3), 0, _evening.AddHours(-2)));
            await store.CreateAsync(Make(null, "c1", _evening.AddDays(-9), 2, _evening.AddDays(-3)));

            var stopped = Make(null, "c1", _evening.AddDays(-3), 0, null);
            stopped.Put(new SurveyInvitationReminderFailures { FailedAttempts = 5, Stopped = true });
            await store.CreateAsync(stopped);

            await store.CreateAsync(Make(null, "c2", _evening.AddDays(-3), 0, null));

            return true;
        });

        // Act
        var visited = await harness.RunAsync(async services =>
        {
            var ids = new List<string>();
            await services.GetRequiredService<SurveyInvitationStore>().VisitDueForReminderAsync(
                "c1",
                [SurveyInvitationStatus.Sent, SurveyInvitationStatus.Opened, SurveyInvitationStatus.Started],
                _evening.AddDays(-1),
                maxReminders: 2,
                invitation =>
                {
                    ids.Add(invitation.ItemId);

                    return Task.CompletedTask;
                });

            return ids;
        });

        // Assert
        Assert.Equal(new[] { dueId, dueAfterReminderId }.Order(StringComparer.Ordinal), visited.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ASurveyBeingDeleted_InvitesAndRemindsNobody()
    {
        // Arrange: an invitation due a reminder, then the survey's delete begins.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1000);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var invited = await InviteAsync(harness, survey, "first@example.com");
        await SetRemindersAsync(harness, invited.Invitation.CollectorId, count: 3, everyDays: 1);

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var deleting = await store.FindByIdAsync(survey.ItemId);
            deleting.DeletingUtc = _evening;
            await store.UpdateAsync(deleting);

            return true;
        });

        var attemptsBefore = sender.Attempts;

        // Act
        harness.Clock.UtcNow = _evening.AddDays(2);
        var reminded = await RemindAsync(harness, invited.Invitation.CollectorId);
        var another = await InviteAsync(harness, survey, "second@example.com");

        // Assert
        Assert.Equal(0, reminded);
        Assert.Equal(CrestApps.OrchardCore.Surveys.SurveyErrorCode.NotFound, another.Error);
        Assert.Equal(attemptsBefore, sender.Attempts);
    }

    [Fact]
    public async Task AnHourlyRun_WhoseOnlyHeldInvitationIsARefusedAddress_StillSendsTheDueReminders_AndTheAddressBounces()
    {
        // Arrange: a list with two daily reminders; two invitations sent three days ago, due a reminder; and a first send
        // held after the mail server refused its address on an earlier run. The mail server refuses that address every
        // time and takes every other one (R7-M6).
        var sender = new RecordingSender { Outcome = invitation => invitation.Email == "refused@example.com" ? SurveyInvitationDelivery.RetryLater : SurveyInvitationDelivery.Delivered };
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var first = await InviteAsync(harness, survey, "due1@example.com");
        await InviteAsync(harness, survey, "due2@example.com");
        var collectorId = first.Invitation.CollectorId;
        var refusedId = IdGenerator.GenerateId();
        await SetRemindersAsync(harness, collectorId, count: 2, everyDays: 1);
        await harness.RunAsync(async services =>
        {
            var refused = new SurveyInvitation
            {
                ItemId = refusedId,
                SurveyId = survey.ItemId,
                CollectorId = collectorId,
                Email = "refused@example.com",
                Status = SurveyInvitationStatus.Pending,
                CreatedUtc = _evening.AddHours(-2),
            };

            refused.Put(new SurveyInvitationHold { HeldUtc = _evening.AddHours(-2), LastFailedUtc = _evening.AddHours(-1), FailedAttempts = 1, LastCountedUtc = _evening.AddHours(-1) });
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(refused);

            return true;
        });
        var sentBefore = sender.Sent.Count;

        // Act: the hourly task, which gives each list a fresh scope that sent nothing before.
        harness.Clock.UtcNow = _evening;
        await RunReminderTaskAsync(harness);
        var remindersOfTheRun = sender.Sent.Count - sentBefore;
        var afterTheRun = await FindAsync(harness, refusedId);

        // An hour later the address is alone in its run: a lone failure uses up an attempt at most once a day.
        harness.Clock.UtcNow = _evening.AddHours(1);
        await RunReminderTaskAsync(harness);
        var afterALoneRun = await FindAsync(harness, refusedId);

        // Then a run a day: the second reminders go out on the first, and the address uses up its attempts.
        for (var day = 1; day <= 3; day++)
        {
            harness.Clock.UtcNow = _evening.AddDays(day);
            await RunReminderTaskAsync(harness);
        }

        var later = await FindAsync(harness, refusedId);

        // Assert: the refused address no longer stops the list's reminders, and it bounces as it did before R6-L8.
        Assert.Equal(2, remindersOfTheRun);
        Assert.True(afterTheRun.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        Assert.Equal(2, hold.FailedAttempts);
        Assert.True(afterALoneRun.TryGet<SurveyInvitationHold>(out var loneHold) && loneHold is not null);
        Assert.Equal(2, loneHold.FailedAttempts);
        Assert.Equal(4, sender.Sent.Count - sentBefore);
        Assert.Equal(2, (await FindAsync(harness, first.Invitation.ItemId)).RemindersSent);
        Assert.Equal(SurveyInvitationStatus.Bounced, later.Status);
    }

    [Fact]
    public async Task AHeldAddressTheMailServerRefuses_FirstInLine_TriesOneMoreAddress_SoTheRunGoesOn()
    {
        // Arrange: two first sends held by yesterday's limit, the first of which the mail server refuses, and an
        // invitation due a reminder (R7-M6: a first failure, with nothing sent yet, is not taken for an outage).
        var sender = new RecordingSender { Outcome = invitation => invitation.Email == "refused@example.com" ? SurveyInvitationDelivery.RetryLater : SurveyInvitationDelivery.Delivered };
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var due = await InviteAsync(harness, survey, "due@example.com");
        var collectorId = due.Invitation.CollectorId;
        var refusedId = IdGenerator.GenerateId();
        var heldId = IdGenerator.GenerateId();
        await SetRemindersAsync(harness, collectorId, count: 1, everyDays: 1);
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();

            foreach (var (id, email) in new[] { (refusedId, "refused@example.com"), (heldId, "held@example.com") })
            {
                var invitation = new SurveyInvitation
                {
                    ItemId = id,
                    SurveyId = survey.ItemId,
                    CollectorId = collectorId,
                    Email = email,
                    Status = SurveyInvitationStatus.Pending,
                    CreatedUtc = _evening.AddDays(-1),
                };

                invitation.Put(new SurveyInvitationHold { HeldUtc = _evening.AddDays(-1) });
                await store.CreateAsync(invitation);
            }

            return true;
        });

        // Act: an hourly run, in a scope that sent nothing before.
        harness.Clock.UtcNow = _evening;
        var sent = await RemindAsync(harness, collectorId);

        // Assert: the other held send and the reminder went out, and the refused address used up one attempt.
        var refused = await FindAsync(harness, refusedId);

        Assert.Equal(2, sent);
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, heldId)).Status);
        Assert.Equal(1, (await FindAsync(harness, due.Invitation.ItemId)).RemindersSent);
        Assert.Equal(SurveyInvitationStatus.Pending, refused.Status);
        Assert.True(refused.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        Assert.Equal(1, hold.FailedAttempts);
    }

    [Fact]
    public async Task ACompletionCommittedWhileAReminderPageIsOut_StaysCompleted_AndGetsNoMoreReminders()
    {
        // Arrange: three invitations sent three days ago on a list with two daily reminders; the second one's respondent
        // has started (R7-M7).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var first = await InviteAsync(harness, survey, "one@example.com");
        var second = await InviteAsync(harness, survey, "two@example.com");
        var third = await InviteAsync(harness, survey, "three@example.com");
        var collectorId = first.Invitation.CollectorId;
        await SetRemindersAsync(harness, collectorId, count: 2, everyDays: 1);
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();
            var started = await store.FindByIdAsync(second.Invitation.ItemId);
            started.Status = SurveyInvitationStatus.Started;
            await store.UpdateAsync(started);

            return true;
        });

        // Act: the run loads its page; while its first reminder goes out, the respondent of another invitation of the page
        // (not the started one) completes, and while the started one's reminder goes out, its respondent does.
        harness.Clock.UtcNow = _evening;
        var attempted = new List<string>();
        string completedBeforeItsTurn = null;
        var sent = await harness.RunAsync(async services =>
        {
            sender.OnAttemptAsync = async invitation =>
            {
                attempted.Add(invitation.ItemId);

                if (attempted.Count == 1)
                {
                    completedBeforeItsTurn = new[] { first.Invitation.ItemId, third.Invitation.ItemId }.First(id => id != invitation.ItemId);
                    await CompleteElsewhereAsync(services, completedBeforeItsTurn);
                }

                if (invitation.ItemId == second.Invitation.ItemId)
                {
                    await CompleteElsewhereAsync(services, second.Invitation.ItemId);
                }
            };

            return await services.GetRequiredService<SurveyInvitationService>().RemindAsync(collectorId, force: false);
        });

        var remindedId = new[] { first.Invitation.ItemId, third.Invitation.ItemId }.Single(id => id != completedBeforeItsTurn);
        var remindedNow = await FindAsync(harness, remindedId);
        var secondNow = await FindAsync(harness, second.Invitation.ItemId);
        var skippedNow = await FindAsync(harness, completedBeforeItsTurn);
        var attemptedInTheRun = attempted.ToList();

        // The next day's run.
        attempted.Clear();
        sender.OnAttemptAsync = invitation =>
        {
            attempted.Add(invitation.ItemId);

            return Task.CompletedTask;
        };
        harness.Clock.UtcNow = _evening.AddDays(1);
        await RemindAsync(harness, collectorId);

        // Assert: the invitation completed before its turn was read as completed and got nothing; the started one's
        // reminder, already out, is recorded on the completed invitation instead of writing back the copy that said
        // Started; and only the open invitation is reminded the next day.
        Assert.Equal(2, sent);
        Assert.Equal(2, attemptedInTheRun.Count);
        Assert.DoesNotContain(completedBeforeItsTurn, attemptedInTheRun);
        Assert.Equal(1, remindedNow.RemindersSent);
        Assert.Equal(SurveyInvitationStatus.Completed, secondNow.Status);
        Assert.Equal(1, secondNow.RemindersSent);
        Assert.Equal(SurveyInvitationStatus.Completed, skippedNow.Status);
        Assert.Equal(0, skippedNow.RemindersSent);
        Assert.Equal([remindedId], attempted);
    }

    [Fact]
    public async Task AResendOfAUsersList_WhileAnotherStillNotifiesThem_NotifiesNobodyTwice()
    {
        // Arrange: no e-mail service, and a list of two users invited three days ago (R7-Mi7).
        var sender = new RecordingSender { CanSendEmail = false };
        var locks = new InvitationEmailLockDouble(TimeSpan.FromMilliseconds(100));
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services => services.AddSingleton<IDistributedLock>(locks));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var collectorId = await CreateUserInvitationsAsync(harness, survey, "u1", "u2");

        Task<(int Sent, bool Busy)> ResendAsync()
            => harness.RunAsync(async services =>
            {
                var invitations = services.GetRequiredService<SurveyInvitationService>();
                var sent = await invitations.RemindAsync(collectorId, force: true);

                return (sent, invitations.AnotherSendIsGoingOut);
            });

        // Act: a resend while another resend of the list still holds its notification lock; then, once that one is done,
        // two more resends the same evening.
        var otherResend = await locks.AcquireLockAsync(SurveyInvitationService.NotificationLockPrefix + collectorId);
        var whileBusy = await ResendAsync();
        await otherResend.DisposeAsync();
        var afterIt = await ResendAsync();
        var again = await ResendAsync();

        // Assert: the resend that found the list busy notified nobody and says why; each user was notified once.
        Assert.Equal((0, true), whileBusy);
        Assert.Equal((2, false), afterIt);
        Assert.Equal((0, false), again);
        Assert.Equal(2, sender.Attempts);
        Assert.Equal(0, locks.Attempts);
    }

    [Fact]
    public async Task UsersAreNotified_AfterTheEmailsAreCommitted_AndTheEmailLockIsLetGoOf()
    {
        // Arrange: a list with an e-mail invitation and a user invitation, both due a reminder (R7-Mi8).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var emailed = await InviteAsync(harness, survey, "emailed@example.com");
        var collectorId = emailed.Invitation.CollectorId;
        await CreateUserInvitationsAsync(harness, survey, "u1");
        await SetRemindersAsync(harness, collectorId, count: 1, everyDays: 1);
        var locks = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IDistributedLock>()));
        bool? heldWhileEmailing = null;
        bool? heldWhileNotifying = null;
        sender.OnAttemptAsync = async invitation =>
        {
            var held = await locks.IsLockAcquiredAsync(SurveyInvitationEmailLimit.LockKey);

            if (invitation.UserId is null)
            {
                heldWhileEmailing = held;
            }
            else
            {
                heldWhileNotifying = held;
            }
        };

        // Act
        harness.Clock.UtcNow = _evening;
        var sent = await RemindAsync(harness, collectorId);

        // Assert: the e-mail went out under the lock; the user was notified after the lock was let go of.
        Assert.Equal(2, sent);
        Assert.True(heldWhileEmailing);
        Assert.False(heldWhileNotifying);
    }

    [Fact]
    public async Task ASendThatRunsPastMidnight_HoldsTheOneTenantLock_AndCountsEachSiteDayOnItsOwn()
    {
        // Arrange: one e-mail a day; 23:59 on 19 November in New York (R7-Mi9).
        var sender = new RecordingSender();
        var locks = new InvitationEmailLockDouble(TimeSpan.FromMilliseconds(100));
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 1), configure: services => services.AddSingleton<IDistributedLock>(locks));
        var beforeMidnight = new DateTime(2026, 11, 20, 4, 59, 0, DateTimeKind.Utc);
        harness.Clock.UtcNow = beforeMidnight;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act: one send e-mails someone before midnight and someone else after it; then another send the same new day.
        var heldAcrossMidnight = await harness.RunAsync(async services =>
        {
            var invitations = services.GetRequiredService<ISurveyInvitationService>();

            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "before@example.com" });
            harness.Clock.UtcNow = beforeMidnight.AddMinutes(2);
            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "after@example.com" });

            return await locks.IsLockAcquiredAsync(SurveyInvitationEmailLimit.LockKey);
        });
        var attemptsOfTheFirstSend = locks.Attempts;
        var later = await InviteAsync(harness, survey, "later@example.com");

        // Assert: the first send held one lock the whole time and got a fresh count for the new day; the next send of
        // that day finds its one e-mail used.
        Assert.True(heldAcrossMidnight);
        Assert.Equal(1, attemptsOfTheFirstSend);
        Assert.Equal(2, sender.Sent.Count);
        Assert.Equal(SurveyInvitationStatus.Pending, (await FindAsync(harness, later.Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task AnHourlyRun_OfAListWithNoEmailDue_TakesNoEmailLock()
    {
        // Arrange: a list whose e-mail invitation was just sent (no reminder due) and whose user invitation is due
        // (R7-Mi11).
        var sender = new RecordingSender();
        var locks = new InvitationEmailLockDouble(TimeSpan.FromMilliseconds(100));
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services => services.AddSingleton<IDistributedLock>(locks));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var emailed = await InviteAsync(harness, survey, "emailed@example.com");
        var collectorId = emailed.Invitation.CollectorId;
        await CreateUserInvitationsAsync(harness, survey, "u1");
        await SetRemindersAsync(harness, collectorId, count: 1, everyDays: 1);
        var attemptsBefore = locks.Attempts;

        // Act: an hourly run that reminds the user, then one with nothing due at all.
        harness.Clock.UtcNow = _evening.AddHours(1);
        var first = await RemindAsync(harness, collectorId);
        harness.Clock.UtcNow = _evening.AddHours(2);
        var second = await RemindAsync(harness, collectorId);

        // Assert: neither run asked for the e-mail lock; only the run with a user due took the list's notification lock.
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(attemptsBefore, locks.Attempts);
        Assert.Equal(1, locks.NotificationAttempts);
    }

    [Fact]
    public async Task ASendThatCreatesItsInvitationsFirst_StartsItsSendingWindowOnceTheyExist()
    {
        // Arrange (R7-Mi11).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;

        // Act: the send takes the lock, spends six minutes creating its invitations, then starts its window.
        var (tookLock, closedBefore, closedAfter, mayStillSend) = await harness.RunAsync(async services =>
        {
            var limit = services.GetRequiredService<SurveyInvitationEmailLimit>();
            var took = await limit.CanSendAsync(TestContext.Current.CancellationToken);

            harness.Clock.UtcNow = _evening.AddMinutes(6);

            var before = limit.IsClosed;

            limit.StartSendingWindow();

            return (took, before, limit.IsClosed, await limit.CanSendAsync(TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.True(tookLock);
        Assert.True(closedBefore);
        Assert.False(closedAfter);
        Assert.True(mayStillSend);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SeveralRefusedHeldAddresses_WithDueReminders_TheRemindersGoOut_AndTheAddressesBounceAfterTheirAttempts(int refusedCount)
    {
        // Arrange: two invitations sent three days ago and due their one reminder, and first sends held after the mail
        // server refused their addresses on an earlier run. It refuses those addresses every time and takes the others
        // (R8-M2: with two or more, each run's two failures in a row were taken for an outage and never counted).
        var sender = new RecordingSender { Outcome = invitation => invitation.Email.StartsWith("refused", StringComparison.Ordinal) ? SurveyInvitationDelivery.RetryLater : SurveyInvitationDelivery.Delivered };
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var first = await InviteAsync(harness, survey, "due1@example.com");
        var second = await InviteAsync(harness, survey, "due2@example.com");
        var collectorId = first.Invitation.CollectorId;
        await SetRemindersAsync(harness, collectorId, count: 1, everyDays: 1);
        var refusedIds = await CreateHeldAsync(harness, survey, collectorId, Enumerable.Range(1, refusedCount).Select(i => $"refused{i}@example.com").ToArray(), failedBefore: true);

        // Act: a run whose service the test reads (as Send reminders now does); then a run every six hours for twelve
        // days. Once the reminders are out, nothing else goes out, so each address counts a try at most once a day.
        harness.Clock.UtcNow = _evening;
        var (sent, mailServerFailed, refused) = await harness.RunAsync(async services =>
        {
            var invitations = services.GetRequiredService<SurveyInvitationService>();
            var count = await invitations.RemindAsync(collectorId, force: false);

            return (count, invitations.MailServerFailed, invitations.RefusedEmails);
        });
        var afterTheFirstRun = new List<SurveyInvitation>();

        foreach (var id in refusedIds)
        {
            afterTheFirstRun.Add(await FindAsync(harness, id));
        }

        for (var run = 1; run <= 48; run++)
        {
            harness.Clock.UtcNow = _evening.AddHours(6 * run);
            await RunReminderTaskAsync(harness);
        }

        // Assert: the reminders went out and the run doesn't say the mail server failed. A refused address is tried after
        // each reminder that went out, and the rest after the reminders (R10-M1), so every refused address was tried and
        // used up an attempt: the first ones confirmed by the reminder after them, the last as the run's single last try
        // or, with three, among the two in a row that stopped the run (lone, but their first counted try). In the end
        // every refused address bounced.
        Assert.Equal(2, sent);
        Assert.False(mailServerFailed);
        Assert.Equal(refusedCount, refused);
        Assert.Equal(1, (await FindAsync(harness, first.Invitation.ItemId)).RemindersSent);
        Assert.Equal(1, (await FindAsync(harness, second.Invitation.ItemId)).RemindersSent);
        Assert.Equal(refusedCount, afterTheFirstRun.Count(invitation => invitation.TryGet<SurveyInvitationHold>(out var hold) && hold?.FailedAttempts == 1));

        foreach (var id in refusedIds)
        {
            Assert.Equal(SurveyInvitationStatus.Bounced, (await FindAsync(harness, id)).Status);
        }
    }

    [Fact]
    public async Task AFirstInvite_WithTwoRefusedAddressesNextToEachOther_HoldsTheRest_SendsThemNextRun_AndTheAddressesBounce()
    {
        // Arrange: a bulk invite whose first two addresses the mail server refuses every time (R8-M2).
        var sender = new RecordingSender { Outcome = invitation => invitation.Email.StartsWith("bad", StringComparison.Ordinal) ? SurveyInvitationDelivery.RetryLater : SurveyInvitationDelivery.Delivered };
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var requests = new[] { "bad1@example.com", "bad2@example.com", "good1@example.com", "good2@example.com" }
            .Select(email => new SurveyInvitationRequest { Email = email })
            .ToList();

        // Act: the invite; the next hourly run; then a run a day.
        var (results, mailServerFailed) = await harness.RunAsync(async services =>
        {
            var invitations = services.GetRequiredService<SurveyInvitationService>();
            var invited = await invitations.InviteManyAsync(survey.ItemId, null, requests, send: true);

            return (invited, invitations.MailServerFailed);
        });
        var attemptsAtInvite = sender.Attempts;
        var collectorId = results[0].Invitation.CollectorId;
        var badAfterInvite = await FindAsync(harness, results[0].Invitation.ItemId);

        harness.Clock.UtcNow = _evening.AddHours(1);
        var nextRun = await RemindAsync(harness, collectorId);
        var badAfterNextRun = await FindAsync(harness, results[1].Invitation.ItemId);

        for (var day = 1; day <= SurveyInvitationHold.MaxSendAttempts; day++)
        {
            harness.Clock.UtcNow = _evening.AddDays(day).AddHours(1);
            await RemindAsync(harness, collectorId);
        }

        // Assert: the invite stopped after the two failures in a row and held the good addresses, each refused address
        // used up one (lone) attempt. The next run interleaved them (R10-M1): good1, bad1, good2, bad2. good2 confirmed
        // bad1, and bad2 counted as the run's single last try, so both used up their second attempt; a run a day then
        // bounced the bad ones.
        Assert.Equal(2, attemptsAtInvite);
        Assert.True(mailServerFailed);
        Assert.True(results[2].Invitation.TryGet<SurveyInvitationHold>(out var heldGood) && heldGood is not null);
        Assert.True(badAfterInvite.TryGet<SurveyInvitationHold>(out var firstHold) && firstHold is not null);
        Assert.Equal(1, firstHold.FailedAttempts);
        Assert.Equal(2, nextRun);
        Assert.True(badAfterNextRun.TryGet<SurveyInvitationHold>(out var nextHold) && nextHold is not null);
        Assert.Equal(2, nextHold.FailedAttempts);
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, results[2].Invitation.ItemId)).Status);
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, results[3].Invitation.ItemId)).Status);
        Assert.Equal(SurveyInvitationStatus.Bounced, (await FindAsync(harness, results[0].Invitation.ItemId)).Status);
        Assert.Equal(SurveyInvitationStatus.Bounced, (await FindAsync(harness, results[1].Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task TwoRefusedHeldAddresses_OnABusyList_BounceAfterFiveHourlyRuns()
    {
        // Arrange: R10-M1, two first sends held after the mail server refused their addresses, which it refuses every
        // time, on a list where two invitations become due their reminder every hour (sent a day before, an hour apart),
        // so every hourly run e-mails two other people.
        var sender = new RecordingSender { Outcome = invitation => invitation.Email.StartsWith("refused", StringComparison.Ordinal) ? SurveyInvitationDelivery.RetryLater : SurveyInvitationDelivery.Delivered };
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-2);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        string collectorId = null;

        for (var hour = 1; hour <= SurveyInvitationHold.MaxSendAttempts; hour++)
        {
            for (var person = 1; person <= 2; person++)
            {
                harness.Clock.UtcNow = _evening.AddDays(-1).AddHours(hour).AddMinutes(-30);
                collectorId = (await InviteAsync(harness, survey, $"due{hour}-{person}@example.com")).Invitation.CollectorId;
            }
        }

        await SetRemindersAsync(harness, collectorId, count: 1, everyDays: 1);
        var refusedIds = await CreateHeldAsync(harness, survey, collectorId, ["refused1@example.com", "refused2@example.com"], failedBefore: true);

        // Act: five hourly runs of the reminder task.
        for (var run = 1; run <= SurveyInvitationHold.MaxSendAttempts; run++)
        {
            harness.Clock.UtcNow = _evening.AddHours(run);
            await RunReminderTaskAsync(harness);
        }

        // Assert: each run tried a refused address after each reminder that went out, so every failure was confirmed (or
        // was the run's single last try) and counted; both addresses bounced after five hourly runs, not after days.
        foreach (var id in refusedIds)
        {
            Assert.Equal(SurveyInvitationStatus.Bounced, (await FindAsync(harness, id)).Status);
        }
    }

    [Fact]
    public async Task ARun_CountsTheFailedBeforeSendsThatGoOutBetweenTheOthers()
    {
        // Arrange: a first send held by yesterday's limit, and one held after the mail server failed it, which the mail
        // server takes today.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var collectorId = (await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().EnsureCollectorAsync(survey))).ItemId;
        await CreateHeldAsync(harness, survey, collectorId, ["held@example.com"], failedBefore: false);
        await CreateHeldAsync(harness, survey, collectorId, ["failed@example.com"], failedBefore: true);

        // Act
        var sent = await RemindAsync(harness, collectorId);

        // Assert: the held send went out, then the failed-before one right after it (R10-M1); both are counted.
        Assert.Equal(2, sent);
        Assert.Equal(2, sender.Sent.Count);
    }

    [Fact]
    public async Task AThrottleThatStartsPartwayThroughARun_UsesUpNoAttemptOfTheAddressItRefused_SoItDoesNotBounce()
    {
        // Arrange: R9-M2, a mail provider that takes one e-mail a run and then throttles (SMTP 421 "too many messages",
        // which the sender maps to RetryLater). Two valid addresses held after the throttle refused them on four earlier
        // runs (one attempt left, counted an hour ago), and a first send held by yesterday's limit, which goes first.
        var takenThisRun = 0;
        var sender = new RecordingSender { Outcome = _ => ++takenThisRun <= 1 ? SurveyInvitationDelivery.Delivered : SurveyInvitationDelivery.RetryLater };
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var collectorId = (await InviteAsync(harness, survey, "earlier@example.com")).Invitation.CollectorId;
        var heldIds = await CreateHeldAsync(harness, survey, collectorId, ["next@example.com"], failedBefore: false);
        var throttledIds = new[] { IdGenerator.GenerateId(), IdGenerator.GenerateId() };
        await harness.RunAsync(async services =>
        {
            for (var i = 0; i < throttledIds.Length; i++)
            {
                var throttled = new SurveyInvitation
                {
                    ItemId = throttledIds[i],
                    SurveyId = survey.ItemId,
                    CollectorId = collectorId,
                    Email = $"valid{i}@example.com",
                    Status = SurveyInvitationStatus.Pending,
                    CreatedUtc = _evening.AddHours(-2),
                };

                throttled.Put(new SurveyInvitationHold { HeldUtc = _evening.AddDays(-4), LastFailedUtc = _evening.AddHours(-1), FailedAttempts = SurveyInvitationHold.MaxSendAttempts - 1, LastCountedUtc = _evening.AddHours(-1) });
                await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(throttled);
            }

            return true;
        });
        takenThisRun = 0;

        // Act: the hourly run: the first send goes out, then the throttle refuses both held addresses, two failures in a
        // row, which stop the send as in an outage.
        harness.Clock.UtcNow = _evening;
        await RunReminderTaskAsync(harness);

        // Assert: the failures that stopped the send can't be told from an outage: within a day of their last counted
        // attempt they use up none, and neither valid address is marked Bounced (counted, both would be).
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, heldIds[0])).Status);

        foreach (var throttledId in throttledIds)
        {
            var throttledAfter = await FindAsync(harness, throttledId);

            Assert.Equal(SurveyInvitationStatus.Pending, throttledAfter.Status);
            Assert.True(throttledAfter.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
            Assert.Equal(SurveyInvitationHold.MaxSendAttempts - 1, hold.FailedAttempts);
        }
    }

    [Fact]
    public async Task SingleInvitesInOneScope_ADeliveryThenAThrottle_UseUpNoAttemptOfTheThrottledAddresses()
    {
        // Arrange: R10-Mi1, a workflow loop that invites three people in one scope: someone new, then two valid addresses
        // held after a throttle refused them on four earlier runs (one attempt left, counted an hour ago). The provider
        // takes one e-mail and then throttles. The session is saved when the scope ends, as Orchard Core does.
        var takenThisScope = 0;
        var sender = new RecordingSender { Outcome = _ => ++takenThisScope <= 1 ? SurveyInvitationDelivery.Delivered : SurveyInvitationDelivery.RetryLater };
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services => RuntimeTestHarness.UseOrchardSession(services));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var collectorId = (await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().EnsureCollectorAsync(survey))).ItemId;
        var throttledIds = await CreateHeldOneAttemptLeftAsync(harness, survey, collectorId, "valid1@example.com", "valid2@example.com");
        takenThisScope = 0;

        // Act
        await harness.RunInShellScopeAsync(async services =>
        {
            var invitations = services.GetRequiredService<SurveyInvitationService>();

            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "new@example.com" });
            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "valid1@example.com" });
            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "valid2@example.com" });

            return true;
        });

        // Assert: decided when the scope ended, the two failures in a row are taken for an outage (lone, and counted an hour
        // ago), so neither valid address uses up its last attempt. Decided after each invite, the first would have counted.
        foreach (var id in throttledIds)
        {
            var invitation = await FindAsync(harness, id);

            Assert.Equal(SurveyInvitationStatus.Pending, invitation.Status);
            Assert.True(invitation.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
            Assert.Equal(SurveyInvitationHold.MaxSendAttempts - 1, hold.FailedAttempts);
        }
    }

    [Fact]
    public async Task SingleInvitesInOneScope_AFailureThenADelivery_CountTheFailureAsTheAddresses()
    {
        // Arrange: R10-Mi1, a workflow loop that first invites an address the mail server refuses (held, one attempt left,
        // counted an hour ago), then someone new, whose e-mail goes out.
        var sender = new RecordingSender { Outcome = invitation => invitation.Email.StartsWith("refused", StringComparison.Ordinal) ? SurveyInvitationDelivery.RetryLater : SurveyInvitationDelivery.Delivered };
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services => RuntimeTestHarness.UseOrchardSession(services));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var collectorId = (await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().EnsureCollectorAsync(survey))).ItemId;
        var refusedId = (await CreateHeldOneAttemptLeftAsync(harness, survey, collectorId, "refused@example.com"))[0];

        // Act
        await harness.RunInShellScopeAsync(async services =>
        {
            var invitations = services.GetRequiredService<SurveyInvitationService>();

            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "refused@example.com" });
            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "new@example.com" });

            return true;
        });

        // Assert: decided when the scope ended, the e-mail that went out after the failure confirms it was the address's,
        // so it uses up the last attempt and the address bounces (and the count reached the database before the session
        // was saved). Decided at once, it would have been lone, an hour after its last counted try, and counted nothing.
        Assert.Equal(SurveyInvitationStatus.Bounced, (await FindAsync(harness, refusedId)).Status);
    }

    [Fact]
    public async Task SingleInvitesInOneScope_AHeldInviteThenAnUnknownSurvey_KeepTheEmailLock()
    {
        // Arrange: R10-Mi2, the mail server refuses the e-mail, so the invite holds it and marks the failure (writes under
        // the lock) and sends nothing.
        var sender = new RecordingSender { Outcome = _ => SurveyInvitationDelivery.RetryLater };
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act: in one scope, the held invite, then an invite to a survey that doesn't exist.
        var heldAfterUnknown = await harness.RunAsync(async services =>
        {
            var invitations = services.GetRequiredService<SurveyInvitationService>();

            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "ana@example.com" });
            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = "unknown", Email = "bo@example.com" });

            return services.GetRequiredService<SurveyInvitationEmailLimit>().HoldsLock;
        });

        // Assert: the scope wrote the hold under the lock, so the lock is kept until the scope saves; let go, another
        // sender could read the invitation before the hold is committed.
        Assert.True(heldAfterUnknown);
    }

    // Held first sends of e-mail addresses the mail server refused on four earlier runs: one attempt left, the last one
    // counted an hour before _evening.
    private static async Task<string[]> CreateHeldOneAttemptLeftAsync(RuntimeTestHarness harness, Survey survey, string collectorId, params string[] emails)
    {
        var ids = emails.Select(_ => IdGenerator.GenerateId()).ToArray();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();

            for (var i = 0; i < emails.Length; i++)
            {
                var invitation = new SurveyInvitation
                {
                    ItemId = ids[i],
                    SurveyId = survey.ItemId,
                    CollectorId = collectorId,
                    Email = emails[i],
                    Status = SurveyInvitationStatus.Pending,
                    CreatedUtc = _evening.AddDays(-4),
                };

                invitation.Put(new SurveyInvitationHold { HeldUtc = _evening.AddDays(-4), LastFailedUtc = _evening.AddHours(-1), FailedAttempts = SurveyInvitationHold.MaxSendAttempts - 1, LastCountedUtc = _evening.AddHours(-1) });
                await store.CreateAsync(invitation);
            }

            return true;
        });

        return ids;
    }

    [Fact]
    public async Task ASingleInvite_ThatSentNoEmail_LetsGoOfTheEmailLockAtOnce_AndOneThatSentAnEmailKeepsItUntilItsScopeSaves()
    {
        // Arrange: R9-Mi4, a single invite (a workflow's Send Survey Invitation task) takes the tenant's e-mail lock
        // before it reads; one to an unknown survey sends nothing.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act
        var (notFound, heldAfterNothingSent) = await harness.RunAsync(async services =>
        {
            var result = await services.GetRequiredService<SurveyInvitationService>().InviteAsync(new SurveyInvitationRequest { SurveyId = "unknown", Email = "ana@example.com" });

            return (result, services.GetRequiredService<SurveyInvitationEmailLimit>().HoldsLock);
        });
        var heldAfterAnEmail = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyInvitationService>().InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "bo@example.com" });

            return services.GetRequiredService<SurveyInvitationEmailLimit>().HoldsLock;
        });

        // Assert: with nothing e-mailed there is nothing to count, so the lock is let go of before the scope ends; once
        // an e-mail went out it is kept until the scope saves, so no other sender counts the day without it.
        Assert.Equal(SurveyErrorCode.NotFound, notFound.Error);
        Assert.False(heldAfterNothingSent);
        Assert.True(heldAfterAnEmail);
    }

    [Fact]
    public async Task ARespondentWriteFromAnotherScope_DuringARun_IsNotBlocked_AndNeitherWriteOverwritesTheOther()
    {
        // Arrange: three invitations sent three days ago, due their one reminder (R8-M3).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var ids = new List<string>();

        foreach (var email in new[] { "one@example.com", "two@example.com", "three@example.com" })
        {
            ids.Add((await InviteAsync(harness, survey, email)).Invitation.ItemId);
        }

        var collectorId = (await FindAsync(harness, ids[0])).CollectorId;
        await SetRemindersAsync(harness, collectorId, count: 1, everyDays: 1);
        var attempted = new List<string>();
        var otherWrites = new List<TimeSpan>();

        // While each reminder after the first is with the mail server, the respondent of the invitation reminded just
        // before opens it, and the respondent of this one completes it, each in a scope of its own (its own connection).
        sender.OnAttemptAsync = async invitation =>
        {
            attempted.Add(invitation.ItemId);

            if (attempted.Count < 2)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            await WriteElsewhereAsync(harness, attempted[^2], current => current.Status = current.Status == SurveyInvitationStatus.Sent ? SurveyInvitationStatus.Opened : current.Status);
            await WriteElsewhereAsync(harness, invitation.ItemId, current => current.Status = SurveyInvitationStatus.Completed);
            otherWrites.Add(stopwatch.Elapsed);
        };

        // Act
        harness.Clock.UtcNow = _evening;
        var sent = await RemindAsync(harness, collectorId);

        // Assert: the other scopes' writes went through at once during the run; the reminders of the invitations opened
        // after their reminder are kept, and the completions are kept with the reminder recorded on them.
        Assert.Equal(3, sent);
        Assert.Equal(2, otherWrites.Count);
        Assert.All(otherWrites, elapsed => Assert.True(elapsed < TimeSpan.FromSeconds(10), $"A respondent's write took {elapsed}."));

        var firstNow = await FindAsync(harness, attempted[0]);
        var secondNow = await FindAsync(harness, attempted[1]);
        var thirdNow = await FindAsync(harness, attempted[2]);

        Assert.Equal((SurveyInvitationStatus.Opened, 1), (firstNow.Status, firstNow.RemindersSent));
        Assert.Equal((SurveyInvitationStatus.Completed, 1), (secondNow.Status, secondNow.RemindersSent));
        Assert.Equal((SurveyInvitationStatus.Completed, 1), (thirdNow.Status, thirdNow.RemindersSent));
    }

    [Fact]
    public async Task ARunCancelledMidway_StopsBetweenCommits_AndKeepsTheSentMarks_EvenWhenItsScopeFails()
    {
        // Arrange: four invitations sent three days ago, due their one reminder (R8-M3).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var ids = new List<string>();

        for (var i = 0; i < 4; i++)
        {
            ids.Add((await InviteAsync(harness, survey, $"guest{i}@example.com")).Invitation.ItemId);
        }

        var collectorId = (await FindAsync(harness, ids[0])).CollectorId;
        await SetRemindersAsync(harness, collectorId, count: 1, everyDays: 1);
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        sender.OnAttempt = _ =>
        {
            if (++attempts == 2)
            {
                cancellation.Cancel();
            }
        };

        // Act: the run is cancelled while its second reminder is with the mail server (a closed tab, a proxy timeout);
        // then its scope fails and its session is cancelled, as Orchard Core does on an exception.
        harness.Clock.UtcNow = _evening;
        var (sent, lockHeldAfter) = await harness.RunAsync(async services =>
        {
            var count = await services.GetRequiredService<SurveyInvitationService>().RemindAsync(collectorId, force: false, cancellation.Token);
            var held = await services.GetRequiredService<IDistributedLock>().IsLockAcquiredAsync(SurveyInvitationEmailLimit.LockKey);

            await services.GetRequiredService<ISession>().CancelAsync();

            return (count, held);
        });

        var reminded = new List<int>();

        foreach (var id in ids)
        {
            reminded.Add((await FindAsync(harness, id)).RemindersSent);
        }

        // Assert: the reminder already out finished and both sent marks stayed; nothing more was sent; the lock is free.
        Assert.Equal(2, sent);
        Assert.Equal(2, attempts);
        Assert.Equal(2, reminded.Sum());
        Assert.False(lockHeldAfter);
    }

    [Fact]
    public async Task AFailureLaterInARun_KeepsTheMarksOfTheEmailsSentBeforeIt()
    {
        // Arrange: three invitations due their reminder; the mail service throws on the third (R8-M3). The session is
        // registered as Orchard Core does: cancelled when the scope fails.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services => RuntimeTestHarness.UseOrchardSession(services));
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var ids = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            ids.Add((await InviteAsync(harness, survey, $"guest{i}@example.com")).Invitation.ItemId);
        }

        var collectorId = (await FindAsync(harness, ids[0])).CollectorId;
        await SetRemindersAsync(harness, collectorId, count: 1, everyDays: 1);
        var attempts = 0;
        sender.OnAttemptAsync = _ => ++attempts == 3 ? throw new InvalidOperationException("The mail service failed.") : Task.CompletedTask;

        // Act
        harness.Clock.UtcNow = _evening;
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunInShellScopeAsync(services => services.GetRequiredService<SurveyInvitationService>().RemindAsync(collectorId, force: false)));

        var reminded = new List<int>();

        foreach (var id in ids)
        {
            reminded.Add((await FindAsync(harness, id)).RemindersSent);
        }

        // Assert: the two reminders sent before the failure are recorded; the third is still due.
        Assert.Equal(2, reminded.Sum());
        Assert.Equal(1, reminded.Count(count => count == 0));
    }

    [Fact]
    public async Task AReissuedToken_IsSavedOnTheCurrentCopy_SoACompletionDuringTheSendIsKept()
    {
        // Arrange: an invitation sent three days ago whose stored token hash no longer matches (as after a secret reset),
        // due a reminder (R8-Mi9).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening.AddDays(-3);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var invited = await InviteAsync(harness, survey, "renewed@example.com");
        var expectedHash = invited.Invitation.TokenHash;
        var collectorId = invited.Invitation.CollectorId;
        await SetRemindersAsync(harness, collectorId, count: 1, everyDays: 1);
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();
            var invitation = await store.FindByIdAsync(invited.Invitation.ItemId);
            invitation.TokenHash = "stale";
            await store.UpdateAsync(invitation);

            return true;
        });

        // The respondent completes, in a scope of their own, while the reminder is with the mail server.
        sender.OnAttemptAsync = invitation => WriteElsewhereAsync(harness, invitation.ItemId, current => current.Status = SurveyInvitationStatus.Completed);

        // Act
        harness.Clock.UtcNow = _evening;
        var sent = await RemindAsync(harness, collectorId);
        var now = await FindAsync(harness, invited.Invitation.ItemId);

        // Assert: the completion is kept, with the reminder and the re-issued hash recorded on it.
        Assert.Equal(1, sent);
        Assert.Equal(SurveyInvitationStatus.Completed, now.Status);
        Assert.Equal(1, now.RemindersSent);
        Assert.Equal(expectedHash, now.TokenHash);
    }

    [Fact]
    public async Task AHold_IsWrittenOnTheCurrentCopy_SoAnInvitationOpenedMeanwhileIsNotWrittenBackAsPending()
    {
        // Arrange: one e-mail a day and three invitations created but not sent (R8-Mi10).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var ids = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            ids.Add((await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = $"guest{i}@example.com" }))).Invitation.ItemId);
        }

        var collectorId = (await FindAsync(harness, ids[0])).CollectorId;

        // While the first goes out, the third is opened through its link (shared by hand), in a scope of its own.
        sender.OnAttemptAsync = invitation => invitation.ItemId == ids[0]
            ? WriteElsewhereAsync(harness, ids[2], current => current.Status = SurveyInvitationStatus.Opened)
            : Task.CompletedTask;

        // Act: Send; the limit stops it after the first, so the others are held.
        var sent = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().SendPendingAsync(collectorId));
        var secondNow = await FindAsync(harness, ids[1]);
        var thirdNow = await FindAsync(harness, ids[2]);

        // Assert
        Assert.Equal(1, sent);
        Assert.True(secondNow.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        Assert.Equal(SurveyInvitationStatus.Opened, thirdNow.Status);
        Assert.False(thirdNow.TryGet<SurveyInvitationHold>(out var openedHold) && openedHold is not null);
    }

    [Fact]
    public async Task AnInvite_WhileAnotherSendHoldsTheLock_CreatesTheNewInvitations_AndLeavesExistingOnesAsTheyAre()
    {
        // Arrange: an invitation that bounced, then another sender holds the e-mail lock (R8-Mi11).
        var sender = new RecordingSender { Outcome = _ => SurveyInvitationDelivery.Undeliverable };
        var locks = new InvitationEmailLockDouble(TimeSpan.FromMilliseconds(100));
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services => services.AddSingleton<IDistributedLock>(locks));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var bounced = await InviteAsync(harness, survey, "bounced@example.com");
        sender.Outcome = null;
        var otherSender = await locks.AcquireLockAsync(DayLockKey);

        // Act: the Invite page's Send now, with the bounced address and a new one; then a single invite (a workflow).
        var (results, busy) = await harness.RunAsync(async services =>
        {
            var invitations = services.GetRequiredService<SurveyInvitationService>();
            var invited = await invitations.InviteManyAsync(survey.ItemId, null, [new SurveyInvitationRequest { Email = "bounced@example.com" }, new SurveyInvitationRequest { Email = "new@example.com" }], send: true);

            return (invited, invitations.AnotherSendIsGoingOut);
        });
        var single = await InviteAsync(harness, survey, "bounced@example.com");
        await otherSender.DisposeAsync();

        // Assert: the existing invitation was left as it was and says Busy; the new one was created and held.
        Assert.True(busy);
        Assert.Equal(SurveyErrorCode.Busy, results[0].Error);
        Assert.Equal(SurveyErrorCode.Busy, single.Error);
        Assert.Equal(SurveyInvitationStatus.Bounced, (await FindAsync(harness, bounced.Invitation.ItemId)).Status);
        Assert.True(results[1].Succeeded);
        Assert.True((await FindAsync(harness, results[1].Invitation.ItemId)).TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        Assert.Empty(sender.Sent);
    }

    // Creates first sends held back on a list (after a mail server failure when failedBefore is set); returns their ids.
    private static async Task<IReadOnlyList<string>> CreateHeldAsync(RuntimeTestHarness harness, Survey survey, string collectorId, string[] emails, bool failedBefore)
    {
        var ids = emails.Select(_ => IdGenerator.GenerateId()).ToArray();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();

            for (var i = 0; i < emails.Length; i++)
            {
                var invitation = new SurveyInvitation
                {
                    ItemId = ids[i],
                    SurveyId = survey.ItemId,
                    CollectorId = collectorId,
                    Email = emails[i],
                    Status = SurveyInvitationStatus.Pending,
                    CreatedUtc = _evening.AddHours(-2),
                };

                invitation.Put(new SurveyInvitationHold { HeldUtc = _evening.AddHours(-2), LastFailedUtc = failedBefore ? _evening.AddHours(-1) : null });
                await store.CreateAsync(invitation);
            }

            return true;
        });

        return ids;
    }

    // Writes an invitation as another request does, in a scope (and on a connection) of its own, from a copy it loads now;
    // it fails the test instead of waiting when the send blocks it.
    private static Task<bool> WriteElsewhereAsync(RuntimeTestHarness harness, string invitationId, Action<SurveyInvitation> change)
        => harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();
            var invitation = await store.FindByIdAsync(invitationId);

            change(invitation);
            await store.UpdateAsync(invitation);

            return true;
        }).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

    // Creates a survey's personal links list, with an invitation to each user sent three days ago; returns the list.
    private static async Task<string> CreateUserInvitationsAsync(RuntimeTestHarness harness, Survey survey, params string[] userIds)
    {
        var collector = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().EnsureCollectorAsync(survey));

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();

            foreach (var userId in userIds)
            {
                await store.CreateAsync(new SurveyInvitation
                {
                    ItemId = IdGenerator.GenerateId(),
                    SurveyId = survey.ItemId,
                    CollectorId = collector.ItemId,
                    UserId = userId,
                    Status = SurveyInvitationStatus.Sent,
                    CreatedUtc = harness.Clock.UtcNow.AddDays(-3),
                    SentUtc = harness.Clock.UtcNow.AddDays(-3),
                });
            }

            return true;
        });

        return collector.ItemId;
    }

    // Commits a respondent's completion as another request would, from its own copy loaded after the send loaded its
    // page. SQLite lets one writer in at a time, so this runs in the send's own session: the send's copy is let go of
    // first (the send keeps it, stale), and the other request's copy after its write, so the database has the
    // completion while the send holds a copy without it, as when another server commits it.
    private static async Task CompleteElsewhereAsync(IServiceProvider services, string invitationId)
    {
        var store = services.GetRequiredService<SurveyInvitationStore>();
        var session = services.GetRequiredService<ISession>();
        var sendersCopy = await store.FindByIdAsync(invitationId);

        session.Detach(sendersCopy, SurveysConstants.CollectionName);

        var respondentsCopy = await store.FindByIdAsync(invitationId);

        respondentsCopy.Status = SurveyInvitationStatus.Completed;
        await store.UpdateAsync(respondentsCopy);
        await session.FlushAsync(TestContext.Current.CancellationToken);
        session.Detach(respondentsCopy, SurveysConstants.CollectionName);
    }

    private static Task<bool> RunReminderTaskAsync(RuntimeTestHarness harness)
        => harness.RunInShellAsync(async services =>
        {
            await new SurveyInvitationReminderBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);

            return true;
        });

    private static Task<bool> SetRemindersAsync(RuntimeTestHarness harness, string collectorId, int count, int everyDays)
        => harness.RunAsync(async services =>
        {
            var collectors = services.GetRequiredService<SurveyCollectorStore>();
            var collector = await collectors.FindByIdAsync(collectorId);
            collector.Put(new SurveyInvitationSettings { ReminderCount = count, ReminderEveryDays = everyDays });
            await collectors.UpdateAsync(collector);

            return true;
        });

    [Fact]
    public async Task AFirstSendTheMailServerFails_IsHeldAndSentByALaterRun_NotBounced()
    {
        // Arrange: the mail server is down for the first send.
        var sender = new RecordingSender { Outcome = _ => SurveyInvitationDelivery.RetryLater };
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act
        var invited = await InviteAsync(harness, survey, "down@example.com");
        var whileDown = await FindAsync(harness, invited.Invitation.ItemId);
        sender.Outcome = null;
        harness.Clock.UtcNow = _evening.AddHours(1);
        var later = await RemindAsync(harness, invited.Invitation.CollectorId);

        // Assert
        Assert.Equal(SurveyInvitationStatus.Pending, whileDown.Status);
        Assert.True(whileDown.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);

        // Nothing else of the send was tried, so the server may be down: a lone failure uses up an attempt at most once
        // a day (R7-M6), so an address that is always refused still bounces.
        Assert.Equal(1, hold.FailedAttempts);
        Assert.Equal(_evening, hold.LastFailedUtc);
        Assert.Equal(1, later);
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, invited.Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task AFirstSendThatKeepsFailing_IsBouncedAfterItsAttempts_AndInvitingAgainRetriesIt()
    {
        // Arrange: the mail server refuses this address every time, and takes every other address. Alone in its send,
        // the failure might be an outage, so it uses up an attempt at most once a day (R7-M6).
        var sender = new RecordingSender { Outcome = invitation => invitation.Email == "refused@example.com" ? SurveyInvitationDelivery.RetryLater : SurveyInvitationDelivery.Delivered };
        var refusedAttempts = 0;
        sender.OnAttempt = invitation => refusedAttempts += invitation.Email == "refused@example.com" ? 1 : 0;
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var invited = await InviteAsync(harness, survey, "refused@example.com");
        var alone = await FindAsync(harness, invited.Invitation.ItemId);

        // Act: the hourly runs, each of which e-mails someone else first, try again until the attempts are used up; then
        // the address is fixed and invited again.
        for (var run = 1; run <= SurveyInvitationHold.MaxSendAttempts; run++)
        {
            harness.Clock.UtcNow = _evening.AddHours(run);
            await InviteThenRemindAsync(harness, survey, $"other{run}@example.com", invited.Invitation.CollectorId);
        }

        var afterAttempts = await FindAsync(harness, invited.Invitation.ItemId);
        sender.Outcome = null;
        var again = await InviteAsync(harness, survey, "Refused@Example.com");

        // Assert
        Assert.True(alone.TryGet<SurveyInvitationHold>(out var aloneHold) && aloneHold is not null);
        Assert.Equal(1, aloneHold.FailedAttempts);
        Assert.Equal(SurveyInvitationHold.MaxSendAttempts + 1, refusedAttempts);
        Assert.Equal(SurveyInvitationStatus.Bounced, afterAttempts.Status);
        Assert.Equal(invited.Invitation.ItemId, again.Invitation.ItemId);
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, invited.Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task AMailOutage_TriesTwoEmailsARun_HoldsTheRest_UsesUpAtMostOneAttemptADay_AndEverythingGoesOutOnceItEnds()
    {
        // Arrange: the mail server is down (R6-L8).
        var sender = new RecordingSender { Outcome = _ => SurveyInvitationDelivery.RetryLater };
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var requests = Enumerable.Range(0, 3).Select(i => new SurveyInvitationRequest { Email = $"down{i}@example.com" }).ToList();

        // Act: the invite, then more hourly runs than an invitation has attempts; then the mail server is back.
        var results = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().InviteManyAsync(survey.ItemId, null, requests, send: true));
        var attemptsAtInvite = sender.Attempts;
        var collectorId = results[0].Invitation.CollectorId;

        for (var run = 1; run <= SurveyInvitationHold.MaxSendAttempts + 1; run++)
        {
            harness.Clock.UtcNow = _evening.AddHours(run);
            await RemindAsync(harness, collectorId);
        }

        var attemptsDuringOutage = sender.Attempts;
        var duringOutage = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListByCollectorAsync(collectorId, null, 10));

        sender.Outcome = null;
        harness.Clock.UtcNow = _evening.AddHours(SurveyInvitationHold.MaxSendAttempts + 2);
        var sentOnceBack = await RemindAsync(harness, collectorId);

        // Assert: two tries per send (two failures in a row stop it, R8-M2), no invitation bounced, and as nothing went
        // out each failure is a lone one, so within a few hours each invitation used up one attempt at most (the
        // 23-hour rule); all three go out once it's back.
        Assert.Equal(2, attemptsAtInvite);
        Assert.Equal(2 + (2 * (SurveyInvitationHold.MaxSendAttempts + 1)), attemptsDuringOutage);
        Assert.Equal(3, duringOutage.Count);
        Assert.All(duringOutage, invitation =>
        {
            Assert.Equal(SurveyInvitationStatus.Pending, invitation.Status);
            Assert.True(invitation.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
            Assert.Equal(1, hold.FailedAttempts);
        });
        Assert.Equal(3, sentOnceBack);
        Assert.Equal(3, sender.Sent.Count);
    }

    [Fact]
    public async Task HeldInvitations_KeepGoingOutPageAfterPage_InOneRun_WhileTheLimitAllows()
    {
        // Arrange: more held invitations than one page (R6-L9).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 2000);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var collectorId = (await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "created@example.com" }))).Invitation.CollectorId;
        var heldCount = CrestApps.OrchardCore.Surveys.SurveysConstants.PageSize + 20;

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();

            for (var i = 0; i < heldCount; i++)
            {
                var invitation = new SurveyInvitation
                {
                    ItemId = IdGenerator.GenerateId(),
                    SurveyId = survey.ItemId,
                    CollectorId = collectorId,
                    Email = $"held{i}@example.com",
                    Status = SurveyInvitationStatus.Pending,
                    CreatedUtc = _evening,
                };

                invitation.Put(new SurveyInvitationHold { HeldUtc = _evening });
                await store.CreateAsync(invitation);
            }

            return true;
        });

        // Act
        harness.Clock.UtcNow = _evening.AddHours(1);
        var sent = await RemindAsync(harness, collectorId);

        // Assert: every held invitation went out in the one run.
        var stillHeld = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListHeldByCollectorAsync(collectorId, 10));

        Assert.Equal(heldCount, sent);
        Assert.Equal(heldCount, sender.Sent.Count);
        Assert.Empty(stillHeld);
    }

    [Fact]
    public async Task AManualResend_PastItsSendingWindow_StopsWalkingTheEmailInvitations_AndStillRemindsUsers()
    {
        // Arrange: ten e-mail invitations and one user invitation, sent three days ago, whose stored tokens a resend
        // would re-issue; a mail server that takes two minutes per e-mail (R6-L10).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var collectorId = (await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "created@example.com" }))).Invitation.CollectorId;
        var userInvitationId = IdGenerator.GenerateId();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();

            SurveyInvitation Make(string id, string email, string userId)
                => new()
                {
                    ItemId = id,
                    SurveyId = survey.ItemId,
                    CollectorId = collectorId,
                    Email = email,
                    UserId = userId,
                    Status = SurveyInvitationStatus.Sent,
                    CreatedUtc = _evening.AddDays(-3),
                    SentUtc = _evening.AddDays(-3),
                    TokenHash = "stale",
                };

            for (var i = 0; i < 10; i++)
            {
                await store.CreateAsync(Make(IdGenerator.GenerateId(), $"slow{i}@example.com", null));
            }

            await store.CreateAsync(Make(userInvitationId, null, "u1"));

            return true;
        });

        sender.OnSend = () => harness.Clock.UtcNow = harness.Clock.UtcNow.AddMinutes(2);

        // Act
        await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().RemindAsync(collectorId, force: true));

        // Assert: e-mails go out during the window (at 0, 2 and 4 minutes); the walk of the e-mail invitations then stops,
        // so the other seven are left as they were (their tokens weren't even re-issued). The user is still reminded.
        var all = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListByCollectorAsync(collectorId, [SurveyInvitationStatus.Sent], 100));
        var emails = all.Where(invitation => invitation.UserId is null).ToList();

        Assert.Equal(4, sender.Sent.Count);
        Assert.Equal(3, emails.Count(invitation => invitation.RemindersSent == 1));
        Assert.Equal(7, emails.Count(invitation => invitation.RemindersSent == 0 && invitation.TokenHash == "stale"));
        Assert.Equal(1, all.Single(invitation => invitation.ItemId == userInvitationId).RemindersSent);
    }

    [Fact]
    public async Task TwoSendsOfOneListAtOnce_EmailEachPersonOnce_AndTheLimitCountsEveryEmail()
    {
        // Arrange: three invitations created, not sent; the session is saved as Orchard Core saves it, when the shell
        // scope ends, and slowly (R6-M12).
        var sender = new RecordingSender();
        var locks = new KeyedDistributedLock();
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services =>
        {
            services.AddSingleton<IDistributedLock>(locks);
            RuntimeTestHarness.UseOrchardSession(services, () => Task.Delay(300));
        });
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        string collectorId = null;

        for (var i = 0; i < 3; i++)
        {
            collectorId = (await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = $"twice{i}@example.com" }))).Invitation.CollectorId;
        }

        Task<(int Sent, bool LockUnavailable)> second = null;

        // Act: the first Send e-mails all three in a shell scope (since R8-M3 it commits as it goes, and lets go of the lock
        // once everything is committed). Before its scope ends, a second Send of the list (a double click, or another
        // admin) starts in its own shell scope.
        var first = await harness.RunInShellScopeAsync(async services =>
        {
            var sent = await services.GetRequiredService<SurveyInvitationService>().SendPendingAsync(collectorId);

            using (ExecutionContext.SuppressFlow())
            {
                second = Task.Run(() => harness.RunInShellScopeAsync(async secondServices =>
                {
                    var secondSent = await secondServices.GetRequiredService<SurveyInvitationService>().SendPendingAsync(collectorId);

                    return (secondSent, secondServices.GetRequiredService<SurveyInvitationEmailLimit>().LockUnavailable);
                }));
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);

            return sent;
        });

        var (secondSent, secondLockUnavailable) = await second;

        // Assert: the second Send waited for the lock, read the list after the first one saved, and found nothing to send;
        // each person got one e-mail, and the day's count is every e-mail sent.
        var emailed = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().CountEmailedSinceAsync(SurveyInvitationService.DayStartUtc(_evening, "America/New_York")));
        var all = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListByCollectorAsync(collectorId, null, 10));

        Assert.Equal(3, first);
        Assert.Equal(0, secondSent);
        Assert.False(secondLockUnavailable);
        Assert.Equal(3, sender.Sent.Count);
        Assert.Equal(3, emailed);
        Assert.All(all, invitation =>
        {
            Assert.Equal(SurveyInvitationStatus.Sent, invitation.Status);
            Assert.False(invitation.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        });
    }

    [Fact]
    public async Task AResendThatRunsTwice_RemindsEachPersonOnceADay_AndTheLimitCountsEveryEmail()
    {
        // Arrange: two invitations sent this evening (R6-M12).
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var first = await InviteAsync(harness, survey, "one@example.com");
        await InviteAsync(harness, survey, "two@example.com");
        var collectorId = first.Invitation.CollectorId;

        Task<int> ResendAsync() => harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().RemindAsync(collectorId, force: true));

        // Act: Send reminders now, twice the same evening, then again after midnight in the site's time zone.
        harness.Clock.UtcNow = _evening.AddMinutes(30);
        var firstResend = await ResendAsync();
        harness.Clock.UtcNow = _evening.AddMinutes(31);
        var secondResend = await ResendAsync();
        var emailedToday = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().CountEmailedSinceAsync(SurveyInvitationService.DayStartUtc(_evening, "America/New_York")));
        var sentToday = sender.Sent.Count;
        harness.Clock.UtcNow = _evening.AddHours(7);
        var nextDay = await ResendAsync();

        // Assert
        Assert.Equal(2, firstResend);
        Assert.Equal(0, secondResend);
        Assert.Equal(4, sentToday);
        Assert.Equal(sentToday, emailedToday);
        Assert.Equal(2, nextDay);
    }

    [Fact]
    public async Task WithoutAnEmailService_EmailInvitationsAreHeld_AndTheDailyLockIsNotTaken()
    {
        // Arrange: the Email feature is off.
        var sender = new RecordingSender { CanSendEmail = false };
        var locks = new InvitationEmailLockDouble(TimeSpan.FromMilliseconds(200));
        await using var harness = await CreateAsync(sender, SiteService(emailsPerDay: 100), configure: services => services.AddSingleton<IDistributedLock>(locks));
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act
        var results = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().InviteManyAsync(survey.ItemId, null,
        [
            new SurveyInvitationRequest { Email = "one@example.com" },
            new SurveyInvitationRequest { Email = "two@example.com" },
        ], send: true));
        var reminded = await RemindAsync(harness, results[0].Invitation.CollectorId);

        // Assert: nothing sent or bounced; both wait, held, and no run asked for the lock.
        var held = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListHeldByCollectorAsync(results[0].Invitation.CollectorId, 10));

        Assert.Equal(0, sender.Attempts);
        Assert.Equal(0, reminded);
        Assert.Equal(0, locks.Attempts);
        Assert.Equal(2, held.Count);
        Assert.All(held, invitation => Assert.Equal(SurveyInvitationStatus.Pending, invitation.Status));
    }

    [Fact]
    public async Task InvitingAnAddressAgain_FindsItsInvitationAmongMany_WithoutCaseMattering()
    {
        // Arrange: a list with many invitations, one of them for the address.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1000);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var requests = Enumerable.Range(0, 50).Select(i => new SurveyInvitationRequest { Email = $"many{i}@example.com" }).ToList();
        var first = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().InviteManyAsync(survey.ItemId, null, requests, send: false));

        // Act: the same addresses again, one in other letters, and one twice in the same invite.
        var again = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().InviteManyAsync(survey.ItemId, null,
        [
            new SurveyInvitationRequest { Email = "MANY7@example.com" },
            new SurveyInvitationRequest { Email = "new@example.com" },
            new SurveyInvitationRequest { Email = "new@example.com" },
        ], send: true));
        var all = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().ListByCollectorAsync(first[0].Invitation.CollectorId, null, 1000));

        // Assert: no duplicate invitation, and each address got one e-mail.
        Assert.Equal(first[7].Invitation.ItemId, again[0].Invitation.ItemId);
        Assert.Equal(again[1].Invitation.ItemId, again[2].Invitation.ItemId);
        Assert.Equal(51, all.Count);
        Assert.Equal(2, sender.Sent.Count);
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

    [Fact]
    public async Task OptOuts_AreARecordEach_AndTheOldSingleListIsMovedIntoRecords()
    {
        // Arrange: the old single list holds a plain hash, as sites upgraded from it do; then two new unsubscribes.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (store, protection) = await harness.RunAsync(services => Task.FromResult((services.GetRequiredService<IStore>(), services.GetRequiredService<IDataProtectionProvider>())));

        await using (var seed = store.CreateSession())
        {
            var list = new SurveyInvitationOptOutDocument();
            list.AddressHashes.Add(SurveyInvitationOptOutStore.HashOf("old@example.com"));
            await seed.SaveAsync(list, cancellationToken: TestContext.Current.CancellationToken);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var write = store.CreateSession())
        {
            var optOuts = new SurveyInvitationOptOutStore(write, protection, NullLogger<SurveyInvitationOptOutStore>.Instance);
            await optOuts.SetAsync("a@example.com", optedOut: true, TestContext.Current.CancellationToken);
            await optOuts.SetAsync("b@example.com", optedOut: true, TestContext.Current.CancellationToken);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: the old list is still honored before the move, then the move step empties it into records.
        await using var before = store.CreateSession();
        var oldBeforeMove = await new SurveyInvitationOptOutStore(before, protection, NullLogger<SurveyInvitationOptOutStore>.Instance).GetStateAsync("old@example.com", TestContext.Current.CancellationToken);

        await using (var move = store.CreateSession())
        {
            var step = new CrestApps.OrchardCore.Surveys.Migrations.SurveyInvitationOptOutMoveStep(move);
            Assert.False(await step.RunAsync(new CrestApps.OrchardCore.YesSql.Core.Upgrades.ResumableStepContext(), TestContext.Current.CancellationToken));
            await move.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var after = store.CreateSession();
        var done = await new CrestApps.OrchardCore.Surveys.Migrations.SurveyInvitationOptOutMoveStep(after).RunAsync(new CrestApps.OrchardCore.YesSql.Core.Upgrades.ResumableStepContext(), TestContext.Current.CancellationToken);
        var reader = new SurveyInvitationOptOutStore(after, protection, NullLogger<SurveyInvitationOptOutStore>.Instance);
        var lists = await after.Query<SurveyInvitationOptOutDocument>().ListAsync(TestContext.Current.CancellationToken);
        var records = await after.Query<SurveyInvitationOptOut, SurveyInvitationOptOutIndex>(collection: SurveysConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);

        // Assert: a record per address, the list keeps only its key, and every address is still opted out.
        Assert.Equal(SurveyInvitationOptOutState.OptedOut, oldBeforeMove);
        Assert.True(done);
        Assert.Empty(Assert.Single(lists).AddressHashes);
        Assert.NotNull(Assert.Single(lists).ProtectedKey);
        Assert.Equal(3, records.Count);
        Assert.Equal(SurveyInvitationOptOutState.OptedOut, await reader.GetStateAsync("old@example.com", TestContext.Current.CancellationToken));
        Assert.Equal(SurveyInvitationOptOutState.OptedOut, await reader.GetStateAsync("A@Example.com ", TestContext.Current.CancellationToken));
        Assert.Equal(SurveyInvitationOptOutState.NotOptedOut, await reader.GetStateAsync("c@example.com", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2026-11-19T23:00:00Z", "America/New_York", "2026-11-19T05:00:00Z")]
    [InlineData("2026-11-20T05:30:00Z", "America/New_York", "2026-11-20T05:00:00Z")]
    [InlineData("2026-11-19T23:00:00Z", "", "2026-11-19T00:00:00Z")]
    public void DayStartUtc_IsMidnightInTheSitesTimeZone(string utcNow, string timeZoneId, string expected)
        => Assert.Equal(
            DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
            SurveyInvitationService.DayStartUtc(DateTime.Parse(utcNow, null, System.Globalization.DateTimeStyles.AdjustToUniversal), timeZoneId));

    // The opt-out records live in the Surveys collection, which a harness of another module doesn't create.
    private static async Task AddOptOutTableAsync(IStore store)
    {
        store.RegisterIndexes(new SurveyInvitationOptOutIndexProvider());
        await store.InitializeCollectionAsync(SurveysConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await SurveysIndexSchema.CreateInvitationOptOutTableAsync(new global::YesSql.Sql.SchemaBuilder(store.Configuration, transaction));
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

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

    // One send that invites (and e-mails) someone, then runs a list's reminders, as a run whose mail server takes e-mails.
    private static Task<int> InviteThenRemindAsync(RuntimeTestHarness harness, Survey survey, string email, string collectorId)
        => harness.RunAsync(async services =>
        {
            var invitations = services.GetRequiredService<SurveyInvitationService>();

            await invitations.InviteAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = email });

            return await invitations.RemindAsync(collectorId, force: false);
        });

    private static Task<SurveyInvitation> FindAsync(RuntimeTestHarness harness, string invitationId)
        => harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().FindByIdAsync(invitationId).AsTask());

    private sealed class RecordingSender : ISurveyInvitationSender
    {
        public List<string> Sent { get; } = [];

        public Action OnSend { get; set; }

        // Called for every attempt, delivered or not.
        public Action<SurveyInvitation> OnAttempt { get; set; }

        // Called for every attempt, before its outcome, inside the send's scope (another request's write, for example).
        public Func<SurveyInvitation, Task> OnAttemptAsync { get; set; }

        public bool CanSendEmail { get; set; } = true;

        // The outcome of each send; delivered unless the test says otherwise.
        public Func<SurveyInvitation, SurveyInvitationDelivery> Outcome { get; set; }

        public int Attempts { get; private set; }

        public async Task<SurveyInvitationDelivery> SendAsync(Survey survey, SurveyCollector collector, SurveyInvitation invitation, string link, bool isReminder, CancellationToken cancellationToken = default)
        {
            if (OnAttemptAsync is not null)
            {
                await OnAttemptAsync(invitation);
            }

            var outcome = Outcome?.Invoke(invitation) ?? SurveyInvitationDelivery.Delivered;

            lock (Sent)
            {
                Attempts++;
                OnAttempt?.Invoke(invitation);

                if (outcome == SurveyInvitationDelivery.Delivered)
                {
                    Sent.Add(link);
                }
            }

            OnSend?.Invoke();

            return outcome;
        }
    }

    /// <summary>
    /// Locks whose daily e-mail limit lock another sender always holds: every attempt to take it gives up. Other keys
    /// (the opt-out list's, for example) behave as usual.
    /// </summary>
    private sealed class BusyDistributedLock : IDistributedLock
    {
        private const string BusyPrefix = SurveyInvitationEmailLimit.LockKey;

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
