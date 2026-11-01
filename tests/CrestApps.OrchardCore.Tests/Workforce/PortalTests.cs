using System.Net;
using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.Equal(WorkforceNotificationKind.SchedulePublished, anaInbox.Last().Kind);
        Assert.Equal(WorkforceNotificationKind.ShiftChanged, benInbox.First().Kind);
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

    private static Task<CalendarTestHarness> CreateAsync(RecordingChannel channel = null, PushHandler handler = null)
    {
        return WorkforceTests.CreateAsync(services =>
        {
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

        public Task SendAsync(WorkerProfile worker, WorkforceNotification notification, CancellationToken cancellationToken = default)
        {
            Titles.Add(notification.Title);

            return Task.CompletedTask;
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
