using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// A load saved before its schedule was stored in UTC is converted the way the loader converted it: in the site's
/// time zone.
/// </summary>
public sealed class OmnichannelActivityBatchScheduleMigrationsTests
{
    private static readonly TimeZoneInfo _athens = OmnichannelActivityBatchScheduleMigrations.SiteTimeZone("Europe/Athens");

    [Theory]
    [InlineData(2026, 11, 4, 9, 7)] // Winter: two hours ahead.
    [InlineData(2026, 7, 1, 9, 6)]  // Summer: three hours ahead.
    public void ToUtc_ReadsTheTimeInTheSitesTimeZone(int year, int month, int day, int localHour, int utcHour)
    {
        var utc = OmnichannelActivityBatchScheduleMigrations.ToUtc(new DateTime(year, month, day, localHour, 0, 0), _athens);

        Assert.Equal(new DateTime(year, month, day, utcHour, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void ToUtc_ATimeTheClocksSkipped_IsReadAsTheHourAfter()
    {
        // On 29 March 2026 Athens went from 03:00 to 04:00, so 03:30 never happened there.
        var utc = OmnichannelActivityBatchScheduleMigrations.ToUtc(new DateTime(2026, 3, 29, 3, 30, 0), _athens);

        Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void SiteTimeZone_WithNoTimeZone_IsTheServers()
    {
        Assert.Same(TimeZoneInfo.Local, OmnichannelActivityBatchScheduleMigrations.SiteTimeZone(null));
        Assert.Same(TimeZoneInfo.Local, OmnichannelActivityBatchScheduleMigrations.SiteTimeZone("Not/AZone"));
    }

    [Fact]
    public async Task CreateAsync_StoresEveryLoadsScheduleInUtc()
    {
        // Arrange
        var scheduled = new OmnichannelActivityBatch { ItemId = "scheduled", ScheduleAt = new DateTime(2026, 11, 4, 9, 0, 0) };
        var unscheduled = new OmnichannelActivityBatch { ItemId = "unscheduled" };
        var updated = new List<OmnichannelActivityBatch>();

        var catalog = new Mock<ICatalog<OmnichannelActivityBatch>>();
        catalog.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([scheduled, unscheduled]);
        catalog.Setup(x => x.UpdateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()))
            .Callback<OmnichannelActivityBatch, CancellationToken>((batch, _) => updated.Add(batch))
            .Returns(ValueTask.CompletedTask);

        var site = new Mock<ISite>();
        site.SetupGet(x => x.TimeZoneId).Returns("Europe/Athens");

        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var migration = new OmnichannelActivityBatchScheduleMigrations(
            catalog.Object,
            siteService.Object,
            NullLogger<OmnichannelActivityBatchScheduleMigrations>.Instance);

        // Act
        var version = await migration.CreateAsync();

        // Assert
        Assert.Equal(1, version);
        Assert.Equal(new DateTime(2026, 11, 4, 7, 0, 0, DateTimeKind.Utc), scheduled.ScheduleAt);

        // A load with no schedule is left as it is.
        Assert.Same(scheduled, Assert.Single(updated));
        Assert.Equal(DateTime.MinValue, unscheduled.ScheduleAt);
    }
}
