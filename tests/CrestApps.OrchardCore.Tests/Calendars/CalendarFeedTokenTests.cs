using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// SEC-P10i and DRY-2 step 3: a calendar's private feed secret was stored, indexed and looked up in plain text, so
/// anyone who could read the database or a backup could open every private calendar. The calendar keeps the secret's
/// hash (what the feed is found by) and the secret protected with the tenant's keys (so the address can be shown
/// again); an address handed out before keeps working.
/// </summary>
public sealed class CalendarFeedTokenTests
{
    [Fact]
    public async Task ANewFeedSecret_IsNotStoredInPlainText_AndOpensTheFeed()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var protection = new EphemeralDataProtectionProvider();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Planning");

        // Act
        var token = await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<ICalendarStore>();
            var stored = await store.FindByIdAsync(calendar.ItemId);
            var issued = CalendarFeedTokens.Issue(stored, protection);

            await store.UpdateAsync(stored);

            return issued;
        });
        var (found, document, shown) = await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<ICalendarStore>();
            var match = await store.FindByFeedTokenAsync(token);
            var stored = await store.FindByIdAsync(calendar.ItemId);

            return (match, JsonSerializer.Serialize(stored), CalendarFeedTokens.Read(stored, protection));
        }, commit: false);

        // Assert
        Assert.Equal(calendar.ItemId, found?.ItemId);
        Assert.DoesNotContain(token, document, StringComparison.Ordinal);
        Assert.Equal(token, shown);
    }

    [Fact]
    public async Task AFeedAddressGivenBeforeTheSecretWasHashed_StillOpens()
    {
        await using var harness = await CalendarTestHarness.CreateAsync();
        const string legacy = "0123456789abcdef0123456789abcdef0123456789abcdef";
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Legacy", c => c.FeedToken = legacy);

        var found = await harness.RunAsync(services => services.GetRequiredService<ICalendarStore>().FindByFeedTokenAsync(legacy), commit: false);
        var wrong = await harness.RunAsync(services => services.GetRequiredService<ICalendarStore>().FindByFeedTokenAsync("0123456789abcdef0123456789abcdef0123456789abcdee"), commit: false);

        Assert.Equal(calendar.ItemId, found?.ItemId);
        Assert.Null(wrong);
    }
}
