using System.Net;
using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The My Work portal's services on a real SQLite store: notifications through the channels a worker hasn't turned
/// off, one notice per publish and one per later change, decisions on time off and trades reaching the worker,
/// encrypted web push that forgets devices that are gone, the site's push key, and a worker's private calendar feed.
/// </summary>
public sealed class PortalTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task Notifier_UsesEveryChannelTheWorkerHasntTurnedOff_AndTheInboxAlways()
    {
        var channel = new RecordingChannel();
        await using var harness = await CreateAsync(channel);
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);

        await NotifyAsync(harness, ana, WorkforceNotificationKind.ShiftChanged, "First");
        await SettingsAsync(harness, ana, settings => settings.DisabledChannels.Add(RecordingChannel.ChannelName));
        await NotifyAsync(harness, ana, WorkforceNotificationKind.ShiftChanged, "Second");
        await SettingsAsync(harness, ana, settings =>
        {
            settings.DisabledChannels.Clear();
            settings.DisabledKinds.Add(WorkforceNotificationKind.ShiftTrade);
        });
        await NotifyAsync(harness, ana, WorkforceNotificationKind.ShiftTrade, "Third");

        var inbox = await InboxAsync(harness, ana);

        Assert.Equal(["First"], channel.Titles);
        Assert.Equal(["Third", "Second", "First"], inbox.Items.Select(item => item.Title));
        Assert.Equal(3, inbox.Unread);

        await harness.RunAsync(services => services.GetRequiredService<IWorkforceInbox>().MarkAllReadAsync(ana.ItemId));

        Assert.Equal(0, (await InboxAsync(harness, ana)).Unread);
    }

    [Fact]
    public async Task Publishing_TellsEachWorkerOnce_AndALaterChange_TellsTheWorkersItTouches()
    {
        var channel = new RecordingChannel();
        await using var harness = await CreateAsync(channel);
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team);
        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday.AddDays(1), new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, period, ben.ItemId, _monday.AddDays(2), new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, period);

        var afterPublish = (await InboxAsync(harness, ana)).Items.Count;
        var benShift = (await PlanningKit.ListShiftsAsync(harness, team)).Single(shift => PlanningKit.WorkerOf(shift) == ben.ItemId);

        await harness.RunAsync(services => services.GetRequiredService<IShiftService>().ReassignAsync(benShift.ItemId, ana.ItemId, ScheduleChangeInitiator.Employer, "Cover", "manager"));

        // A republish that moves both of Ana's first shifts tells her once.
        await harness.RunAsync(async services =>
        {
            var planning = services.GetRequiredService<ISchedulePlanningService>();

            foreach (var draft in (await planning.ListDraftShiftsAsync(period.PrimaryScenarioId)).Where(draft => draft.WorkerId == ana.ItemId && draft.Date < _monday.AddDays(2)))
            {
                draft.Start = draft.Start.AddHours(1);
                draft.End = draft.End.AddHours(1);
                await planning.SaveDraftShiftAsync(draft, "manager");
            }
        });
        await PlanningKit.PublishAsync(harness, period);

        var anaInbox = (await InboxAsync(harness, ana)).Items;
        var benInbox = (await InboxAsync(harness, ben)).Items;

        Assert.Equal(1, afterPublish);
        Assert.Equal(3, anaInbox.Count);
        Assert.Equal(2, benInbox.Count);
        Assert.All(anaInbox.Concat(benInbox), item => Assert.False(string.IsNullOrEmpty(item.Url)));
        Assert.Equal(WorkforceNotificationKind.SchedulePublished, anaInbox[^1].Kind);
        Assert.Equal(WorkforceNotificationKind.ShiftChanged, benInbox[0].Kind);
    }

    [Fact]
    public async Task TimeOffDecisions_AndOfferedTrades_ReachTheWorker()
    {
        var channel = new RecordingChannel();
        await using var harness = await CreateAsync(channel);
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team);
        var sick = await TimeOffKit.AddTypeAsync(harness, "Sick", type => type.RequiresBalance = false);
        var first = await TimeOffKit.SubmitAsync(harness, ana, sick, _monday.AddDays(3));
        var second = await TimeOffKit.SubmitAsync(harness, ana, sick, _monday.AddDays(4));

        await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(first.Value.ItemId, "manager"));
        await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().DeclineAsync(second.Value.ItemId, "manager", "Busy week"));

        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, period);

        var shift = Assert.Single(await PlanningKit.ListShiftsAsync(harness, team));
        var offered = await harness.RunAsync(services => services.GetRequiredService<IShiftTradeService>().OfferAsync(new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = shift.ItemId, TargetWorkerId = ben.ItemId }, "Ana"));

        Assert.True(offered.Succeeded, string.Join(" ", offered.Errors));

        var anaTimeOff = (await InboxAsync(harness, ana)).Items.Where(item => item.Kind == WorkforceNotificationKind.TimeOff).ToArray();
        var benTrades = (await InboxAsync(harness, ben)).Items.Where(item => item.Kind == WorkforceNotificationKind.ShiftTrade).ToArray();

        Assert.Equal(2, anaTimeOff.Length);
        Assert.Contains(anaTimeOff, item => item.Body.Contains("Busy week", StringComparison.Ordinal));
        Assert.Single(benTrades);
    }

    [Fact]
    public async Task WebPush_SendsEncryptedMessages_AndForgetsDevicesThatAreGone()
    {
        var handler = new PushHandler();
        await using var harness = await CreateAsync(handler: handler);
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var (p256dh, auth) = DeviceKeys();

        await harness.RunAsync(async services =>
        {
            var push = services.GetRequiredService<IWebPushService>();

            await push.SubscribeAsync(ana.ItemId, "https://push.example/kept", p256dh, auth, "Test browser");
            await push.SubscribeAsync(ana.ItemId, "https://push.example/gone", p256dh, auth, "Old browser");
            await push.SubscribeAsync(ana.ItemId, "https://push.example/kept", p256dh, auth, "Test browser");
        });

        await NotifyAsync(harness, ana, WorkforceNotificationKind.ShiftChanged, "Your shift moved");

        var devices = await harness.RunAsync(services => services.GetRequiredService<IWebPushService>().ListAsync(ana.ItemId), commit: false);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("aes128gcm", request.ContentEncoding);
            Assert.StartsWith("vapid ", request.Authorization, StringComparison.Ordinal);
            Assert.True(request.BodyLength > 0);
        });

        var kept = Assert.Single(devices);

        Assert.Equal("https://push.example/kept", kept.Endpoint);
        Assert.NotNull(kept.LastSentUtc);
    }

    [Theory]
    [InlineData("https://127.0.0.1/push")]
    [InlineData("https://localhost/push")]
    [InlineData("https://10.0.0.5/push")]
    [InlineData("https://192.168.1.10/push")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://[::1]/push")]
    [InlineData("https://[fe80::1]/push")]
    [InlineData("http://push.example/plain-text")]
    [InlineData("https://user:secret@push.example/credentials")]
    public async Task WebPush_RefusesAnEndpointOnTheSitesOwnNetwork(string endpoint)
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var (p256dh, auth) = DeviceKeys();

        var subscription = await harness.RunAsync(services => services.GetRequiredService<IWebPushService>().SubscribeAsync(ana.ItemId, endpoint, p256dh, auth, "Test browser"));
        var devices = await harness.RunAsync(services => services.GetRequiredService<IWebPushService>().ListAsync(ana.ItemId), commit: false);

        Assert.Null(subscription);
        Assert.Empty(devices);
    }

    [Fact]
    public async Task WebPush_RefusesAHostOutsideTheAllowedPushServices_WhenTheyAreListed()
    {
        await using var harness = await CreateAsync(configure: options => options.AllowedHosts = ["fcm.googleapis.com", "*.push.apple.com"]);
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var (p256dh, auth) = DeviceKeys();

        await harness.RunAsync(async services =>
        {
            var push = services.GetRequiredService<IWebPushService>();

            Assert.Null(await push.SubscribeAsync(ana.ItemId, "https://push.example/device", p256dh, auth, "Test browser"));
            Assert.NotNull(await push.SubscribeAsync(ana.ItemId, "https://fcm.googleapis.com/fcm/send/device", p256dh, auth, "Test browser"));
            Assert.NotNull(await push.SubscribeAsync(ana.ItemId, "https://web.push.apple.com/device", p256dh, auth, "Test browser"));
        });
    }

    [Fact]
    public async Task WebPush_TheEleventhDevice_ReplacesTheOldest()
    {
        var clock = new MovableClock();
        await using var harness = await CreateAsync(clock: clock);
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var (p256dh, auth) = DeviceKeys();

        for (var i = 1; i <= 11; i++)
        {
            clock.UtcNow = clock.UtcNow.AddMinutes(1);
            var endpoint = "https://push.example/device-" + i;

            await harness.RunAsync(services => services.GetRequiredService<IWebPushService>().SubscribeAsync(ana.ItemId, endpoint, p256dh, auth, "Test browser"));
        }

        var devices = await harness.RunAsync(services => services.GetRequiredService<IWebPushService>().ListAsync(ana.ItemId), commit: false);

        Assert.Equal(10, devices.Count);
        Assert.DoesNotContain(devices, device => device.Endpoint == "https://push.example/device-1");
        Assert.Contains(devices, device => device.Endpoint == "https://push.example/device-11");
    }

    [Fact]
    public async Task WebPush_ParallelSubscribes_StayWithinTheCap()
    {
        // Arrange: one worker fires many subscribes at once, each with its own endpoint.
        await using var harness = await CreateAsync(distributedLock: new global::OrchardCore.Locking.LocalLock(NullLogger<global::OrchardCore.Locking.LocalLock>.Instance));
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var (p256dh, auth) = DeviceKeys();

        // Act
        await Task.WhenAll(Enumerable.Range(1, 25).Select(i => Task.Run(
            () => harness.RunAsync(services => services.GetRequiredService<IWebPushService>().SubscribeAsync(ana.ItemId, "https://push.example/parallel-" + i, p256dh, auth, "Test browser")),
            TestContext.Current.CancellationToken)));

        var devices = await harness.RunAsync(services => services.GetRequiredService<IWebPushService>().ListAsync(ana.ItemId), commit: false);

        // Assert
        Assert.Equal(10, devices.Count);
    }

    [Fact]
    public async Task WebPush_ANotification_GoesToTheNewestDevicesUpToTheCap_AndOnlyToAllowedHosts()
    {
        // Arrange: devices stored before the cap existed, or before the allowed hosts were tightened, are still there.
        var handler = new PushHandler();
        var clock = new MovableClock();
        await using var harness = await CreateAsync(handler: handler, clock: clock, configure: options => options.AllowedHosts = ["push.example"]);
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var (p256dh, auth) = DeviceKeys();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<WebPushSubscriptionStore>();

            for (var i = 1; i <= 15; i++)
            {
                await store.CreateAsync(new WebPushSubscription
                {
                    ItemId = UniqueId.GenerateId(),
                    WorkerId = ana.ItemId,
                    Endpoint = (i == 15 ? "https://elsewhere.example/device-" : "https://push.example/device-") + i,
                    P256dh = p256dh,
                    Auth = auth,
                    CreatedUtc = clock.UtcNow.AddMinutes(i),
                });
            }
        });

        // Act
        await NotifyAsync(harness, ana, WorkforceNotificationKind.ShiftChanged, "Your shift moved");

        // Assert: the ten newest, less the one whose host is no longer allowed.
        Assert.Equal(9, handler.Requests.Count);
    }

    [Fact]
    public async Task WebPush_TheClientRefusesAHostOnTheSitesOwnNetwork_AtConnect()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkforcePortalCore();
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(WorkforceConstants.WebPushHttpClient);

        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://127.0.0.1:9/push", TestContext.Current.CancellationToken));

        Assert.Contains(global::CrestApps.OrchardCore.Core.Http.EgressNetwork.RefusedMessage, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushKey_IsMadeOnce()
    {
        await using var harness = await CreateAsync();

        var first = await harness.RunAsync(services => services.GetRequiredService<IWebPushService>().GetPublicKeyAsync());
        var second = await harness.RunAsync(services => services.GetRequiredService<IWebPushService>().GetPublicKeyAsync());

        Assert.Equal(first, second);
        Assert.Equal(65, Base64UrlDecode(first).Length);
    }

    [Fact]
    public async Task Feed_HasTheWorkersShiftsAndTimeOff_ForTheirSecretOnly()
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team);
        var period = await PlanningKit.CreatePeriodAsync(harness, team);
        var sick = await TimeOffKit.AddTypeAsync(harness, "Sick", type => type.RequiresBalance = false);

        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, period, ben.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, period);

        var request = await TimeOffKit.SubmitAsync(harness, ana, sick, _monday.AddDays(2));

        await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(request.Value.ItemId, "manager"));

        var token = await harness.RunAsync(async services => await services.GetRequiredService<IWorkerFeedService>().GetTokenAsync(await services.GetRequiredService<IWorkerManager>().FindByIdAsync(ana.ItemId), false));
        var feed = await harness.RunAsync(services => services.GetRequiredService<IWorkerFeedService>().WriteAsync(token), commit: false);
        var renewed = await harness.RunAsync(async services => await services.GetRequiredService<IWorkerFeedService>().GetTokenAsync(await services.GetRequiredService<IWorkerManager>().FindByIdAsync(ana.ItemId), true));
        var old = await harness.RunAsync(services => services.GetRequiredService<IWorkerFeedService>().WriteAsync(token), commit: false);
        var current = await harness.RunAsync(services => services.GetRequiredService<IWorkerFeedService>().WriteAsync(renewed), commit: false);

        Assert.StartsWith("BEGIN:VCALENDAR", feed, StringComparison.Ordinal);
        Assert.Equal(2, CountOf(feed, "BEGIN:VEVENT"));
        Assert.Contains("Front desk", feed, StringComparison.Ordinal);
        Assert.Contains("Sick", feed, StringComparison.Ordinal);
        Assert.NotEqual(token, renewed);
        Assert.Null(old);
        Assert.NotNull(current);
        Assert.Null(await harness.RunAsync(services => services.GetRequiredService<IWorkerFeedService>().WriteAsync("not-a-real-secret-at-all"), commit: false));
    }

    // SEC-P10i: the feed secret was stored in the worker's document in plain text, so anyone who could read the database
    // or a backup could open every worker's schedule.
    [Fact]
    public async Task Feed_SecretIsNotStoredInPlainText_AndStillShownAndOpened()
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);

        var token = await harness.RunAsync(async services => await services.GetRequiredService<IWorkerFeedService>().GetTokenAsync(await services.GetRequiredService<IWorkerManager>().FindByIdAsync(ana.ItemId), true));
        var shownAgain = await harness.RunAsync(async services => await services.GetRequiredService<IWorkerFeedService>().GetTokenAsync(await services.GetRequiredService<IWorkerManager>().FindByIdAsync(ana.ItemId), false));
        var document = await harness.RunAsync(async services => System.Text.Json.JsonSerializer.Serialize(await services.GetRequiredService<IWorkerManager>().FindByIdAsync(ana.ItemId)), commit: false);
        var feed = await harness.RunAsync(services => services.GetRequiredService<IWorkerFeedService>().WriteAsync(token), commit: false);

        Assert.Equal(token, shownAgain);
        Assert.DoesNotContain(token, document, StringComparison.Ordinal);
        Assert.NotNull(feed);
    }

    [Fact]
    public async Task Feed_AnAddressGivenBeforeTheSecretWasHashed_StillOpens()
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        const string legacy = "legacy-plain-feed-secret-0123456789abcdef";

        await SettingsAsync(harness, ana, settings => settings.FeedToken = legacy);

        var feed = await harness.RunAsync(services => services.GetRequiredService<IWorkerFeedService>().WriteAsync(legacy), commit: false);

        Assert.NotNull(feed);
    }

    [Fact]
    public async Task Reminders_AreSentOncePerShift_AsLongBeforeAsEachWorkerChose()
    {
        // The time is written in the reader's culture; pin one so the title reads the same on every machine.
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

        try
        {
            var channel = new RecordingChannel();
            var clock = new MovableClock();
            await using var harness = await CreateAsync(channel, clock: clock);
            var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
            var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
            var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team, worker => worker.Put(new WorkerPortalSettings { ReminderMinutes = 0 }));
            var period = await PlanningKit.CreatePeriodAsync(harness, team);

            await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
            await PlanningKit.AddDraftAsync(harness, period, ben.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
            await PlanningKit.PublishAsync(harness, period);
            channel.Titles.Clear();

            Task<int> SendAsync() => harness.RunAsync(services => services.GetRequiredService<ShiftReminderService>().SendDueAsync());

            // 9:00 in New York; Ana is reminded an hour before, Ben never.
            clock.UtcNow = PlanningKit.Utc(_monday, new TimeOnly(7, 30));
            var early = await SendAsync();
            clock.UtcNow = PlanningKit.Utc(_monday, new TimeOnly(8, 10));
            var due = await SendAsync();
            var again = await SendAsync();

            Assert.Equal((0, 1, 0), (early, due, again));
            // Formatted here rather than written out: ICU on Linux puts a narrow no-break space before "AM".
            var startsAt = new DateTime(2026, 1, 1, 9, 0, 0).ToString("t", System.Globalization.CultureInfo.CurrentCulture);

            Assert.Equal([$"Your shift starts at {startsAt}"], channel.Titles);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Reminders_AreNotSentAgain_WhenTheCacheIsLost()
    {
        // Arrange: a restart, or a node with its own memory cache, loses whatever a cache remembered.
        var channel = new RecordingChannel();
        var clock = new MovableClock();
        await using var harness = await CreateAsync(channel, clock: clock, cachePerRun: true);
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, period);
        channel.Titles.Clear();
        clock.UtcNow = PlanningKit.Utc(_monday, new TimeOnly(8, 10));

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<ShiftReminderService>().SendDueAsync());
        var afterRestart = await harness.RunAsync(services => services.GetRequiredService<ShiftReminderService>().SendDueAsync());

        // Assert
        Assert.Equal((1, 0), (first, afterRestart));
        Assert.Single(channel.Titles);
    }

    [Fact]
    public async Task Reminders_TwoRunsAtOnce_SendOneReminder()
    {
        // Arrange: the first run is held while it sends, and a second run starts meanwhile, as on another node.
        var channel = new RecordingChannel();
        var clock = new MovableClock();
        await using var harness = await CreateAsync(channel, clock: clock, distributedLock: new global::OrchardCore.Locking.LocalLock(NullLogger<global::OrchardCore.Locking.LocalLock>.Instance));
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, period);
        channel.Titles.Clear();
        clock.UtcNow = PlanningKit.Utc(_monday, new TimeOnly(8, 10));

        var sending = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        channel.OnSend = async () =>
        {
            sending.TrySetResult();
            await release.Task;
        };

        // Act
        var firstRun = harness.RunAsync(services => services.GetRequiredService<ShiftReminderService>().SendDueAsync());

        await sending.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var second = await harness.RunAsync(services => services.GetRequiredService<ShiftReminderService>().SendDueAsync());

        release.SetResult();

        var first = await firstRun;

        channel.OnSend = null;

        var third = await harness.RunAsync(services => services.GetRequiredService<ShiftReminderService>().SendDueAsync());

        // Assert
        Assert.Equal((1, 0, 0), (first, second, third));
        Assert.Single(channel.Titles);
    }

    [Fact]
    public async Task QuietHours_KeepTheOtherChannelsQuiet_ButTheInboxStillGetsIt()
    {
        var channel = new RecordingChannel();
        var clock = new MovableClock();
        await using var harness = await CreateAsync(channel, clock: clock);
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team, worker => worker.Put(new WorkerPortalSettings { QuietStart = new TimeOnly(22, 0), QuietEnd = new TimeOnly(7, 0) }));

        clock.UtcNow = PlanningKit.Utc(_monday, new TimeOnly(23, 30));
        await NotifyAsync(harness, ana, WorkforceNotificationKind.ShiftChanged, "Late change");
        clock.UtcNow = PlanningKit.Utc(_monday.AddDays(1), new TimeOnly(7, 30));
        await NotifyAsync(harness, ana, WorkforceNotificationKind.ShiftChanged, "Morning change");

        Assert.Equal(["Morning change"], channel.Titles);
        Assert.Equal(2, (await InboxAsync(harness, ana)).Items.Count);
        Assert.True(new WorkerPortalSettings { QuietStart = new TimeOnly(22, 0), QuietEnd = new TimeOnly(7, 0) }.IsQuiet(new TimeOnly(2, 0)));
        Assert.False(new WorkerPortalSettings { QuietStart = new TimeOnly(12, 0), QuietEnd = new TimeOnly(13, 0) }.IsQuiet(new TimeOnly(13, 0)));
    }

    private static Task<CalendarTestHarness> CreateAsync(RecordingChannel channel = null, PushHandler handler = null, MovableClock clock = null, IDistributedLock distributedLock = null, bool cachePerRun = false, Action<WebPushOptions> configure = null)
    {
        return WorkforceTests.CreateAsync(services =>
        {
            if (configure is not null)
            {
                services.Configure(configure);
            }

            if (clock is not null)
            {
                services.AddSingleton<global::OrchardCore.Modules.IClock>(clock);
            }

            if (distributedLock is not null)
            {
                services.Replace(ServiceDescriptor.Singleton(distributedLock));
            }

            if (cachePerRun)
            {
                // Each run gets an empty cache, as after a restart or on a node with a memory cache of its own.
                services.AddScoped<IDistributedCache>(_ => new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
            }
            else
            {
                services.AddDistributedMemoryCache();
            }
            services.AddWorkforceTimeOffCore();
            services.AddWorkforcePortalCore();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddHttpClient(WorkforceConstants.WebPushHttpClient).ConfigurePrimaryHttpMessageHandler(() => handler ?? new PushHandler());

            if (channel is not null)
            {
                services.AddSingleton<IWorkforceNotificationChannel>(channel);
            }
        });
    }

    private static Task NotifyAsync(CalendarTestHarness harness, WorkerProfile worker, WorkforceNotificationKind kind, string title)
    {
        return harness.RunAsync(services => services.GetRequiredService<IWorkforceNotifier>().NotifyAsync(new WorkforceNotification
        {
            WorkerId = worker.ItemId,
            Kind = kind,
            Title = title,
            Body = title,
            Url = "my-work",
        }));
    }

    private static Task SettingsAsync(CalendarTestHarness harness, WorkerProfile worker, Action<WorkerPortalSettings> change)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IWorkerManager>();
            var stored = await manager.FindByIdAsync(worker.ItemId);
            var settings = stored.TryGet<WorkerPortalSettings>(out var existing) ? existing : new WorkerPortalSettings();

            change(settings);
            stored.Put(settings);
            await manager.UpdateAsync(stored);
        });
    }

    private static Task<(int Count, int Unread, IReadOnlyList<WorkforceInboxItem> Items)> InboxAsync(CalendarTestHarness harness, WorkerProfile worker)
    {
        return harness.RunAsync(services => services.GetRequiredService<IWorkforceInbox>().PageAsync(worker.ItemId, 0, 50), commit: false);
    }

    private static (string P256dh, string Auth) DeviceKeys()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;

        return (Base64UrlEncode([0x04, .. point.X, .. point.Y]), Base64UrlEncode(RandomNumberGenerator.GetBytes(16)));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(padded + new string('=', (4 - (padded.Length % 4)) % 4));
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;

        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private sealed class RecordingChannel : IWorkforceNotificationChannel
    {
        public const string ChannelName = "Recording";

        public List<string> Titles { get; } = [];

        public string Name => ChannelName;

        public string DisplayName => "Recording";

        public Func<Task> OnSend { get; set; }

        public async Task SendAsync(WorkerProfile worker, WorkforceNotification notification, CancellationToken cancellationToken = default)
        {
            lock (Titles)
            {
                Titles.Add(notification.Title);
            }

            if (OnSend is { } onSend)
            {
                await onSend();
            }
        }
    }

    private sealed class PushHandler : HttpMessageHandler
    {
        public List<(string ContentEncoding, string Authorization, long BodyLength)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);

            Requests.Add((string.Join(",", request.Content?.Headers.ContentEncoding ?? []), request.Headers.Authorization?.ToString(), body.LongLength));

            return new HttpResponseMessage(request.RequestUri.AbsolutePath.EndsWith("/gone", StringComparison.Ordinal) ? HttpStatusCode.Gone : HttpStatusCode.Created);
        }
    }
}

internal sealed class MovableClock : global::OrchardCore.Modules.IClock
{
    public DateTime UtcNow { get; set; } = CalendarTestHarness.Now;

    public global::OrchardCore.Modules.ITimeZone[] GetTimeZones() => [];

    public global::OrchardCore.Modules.ITimeZone GetTimeZone(string timeZoneId) => null;

    public global::OrchardCore.Modules.ITimeZone GetSystemTimeZone() => null;

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, global::OrchardCore.Modules.ITimeZone timeZone) => dateTimeOffset;
}
