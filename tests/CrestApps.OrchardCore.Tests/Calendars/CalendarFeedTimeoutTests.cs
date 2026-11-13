using System.Net;
using System.Text;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.External;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// SEC-P5, second review: the iCalendar fetch has its own timeout so one slow feed can't hold up the others. Bug: the
/// timeout surfaced as an <see cref="OperationCanceledException"/>, which the sync run rethrows as a cancellation of the
/// whole run, so every calendar after the slow one was skipped and the slow one recorded no error.
/// </summary>
public sealed class CalendarFeedTimeoutTests
{
    [Fact]
    public async Task ASlowFeed_FailsOnItsOwn_AndTheOtherCalendarsStillSync()
    {
        // Arrange: the real iCalendar provider and sync service on a SQLite store, with a feed that never answers.
        await using var harness = await CalendarTestHarness.CreateAsync(services =>
        {
            services.AddCalendarSync();
            services.AddScoped<ICalendarProvider>(_ => new ICalendarUrlProvider(CreateFactory(), TimeSpan.FromMilliseconds(200)));
        });
        var slow = await SubscribeAsync(harness, "https://calendar.example.com/slow.ics");
        var healthy = await SubscribeAsync(harness, "https://calendar.example.com/healthy.ics");

        // Act
        var synced = await harness.RunAsync(services => services.GetRequiredService<CalendarSyncService>().SyncDueAsync(TestContext.Current.CancellationToken));

        // Assert: the run went through both calendars instead of stopping at the slow one.
        Assert.Equal(2, synced);

        var failed = (await FindCalendarAsync(harness, slow.ItemId)).GetOrCreate<CalendarSubscription>();
        var succeeded = (await FindCalendarAsync(harness, healthy.ItemId)).GetOrCreate<CalendarSubscription>();

        Assert.Equal(1, failed.Failures);
        Assert.False(string.IsNullOrEmpty(failed.LastError));
        Assert.Equal(0, succeeded.Failures);
        Assert.NotNull(succeeded.LastSyncedUtc);
    }

    [Fact]
    public async Task ACanceledRun_StillSurfacesAsACancellation()
    {
        // Arrange
        var provider = new ICalendarUrlProvider(CreateFactory(), TimeSpan.FromSeconds(30));
        using var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act and assert: the caller's own cancellation is not turned into a timeout.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.PullAsync(
            new Calendar { TimeZoneId = "UTC" },
            new CalendarSubscription { Url = "https://calendar.example.com/slow.ics" },
            canceled.Token));
    }

    private static IHttpClientFactory CreateFactory()
    {
        var factory = new Mock<IHttpClientFactory>();

        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new FeedHandler()));

        return factory.Object;
    }

    private static Task<Calendar> SubscribeAsync(CalendarTestHarness harness, string url)
    {
        return harness.CreateCalendarAsync(CalendarTestHarness.General, "Feed " + Guid.NewGuid().ToString("N"), calendar =>
        {
            calendar.TimeZoneId = "UTC";
            calendar.Put(new CalendarSubscription { Provider = ICalendarUrlProvider.ProviderName, Url = url });
        });
    }

    private static Task<Calendar> FindCalendarAsync(CalendarTestHarness harness, string calendarId)
    {
        return harness.RunAsync(async services => await services.GetRequiredService<ICalendarService>().FindByIdAsync(calendarId, TestContext.Current.CancellationToken), commit: false);
    }

    // The slow feed sends nothing until the request is canceled; the healthy one answers with one event.
    private sealed class FeedHandler : HttpMessageHandler
    {
        private const string Feed = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Test//EN\r\nBEGIN:VEVENT\r\nUID:a\r\nDTSTAMP:20261110T090000Z\r\nDTSTART:20261110T090000Z\r\nDTEND:20261110T100000Z\r\nSUMMARY:Planning\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri.AbsolutePath.Contains("slow", StringComparison.Ordinal))
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Feed, Encoding.UTF8, "text/calendar"),
            };
        }
    }
}
