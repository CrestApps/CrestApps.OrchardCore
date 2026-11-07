using CrestApps.OrchardCore.Bookings.Deployments;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Recipes;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// The booking dates deployment step writes the upcoming open dates in the shape the BookingDates recipe step reads, so
/// an export imports into an empty site with the same dates, capacity and prices, and a second run adds nothing.
/// </summary>
public sealed class BookingDatesDeploymentTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpcomingOpenDates_ExportAndImportIntoAnEmptySite_WithoutDuplicates()
    {
        // Arrange
        var source = new InMemorySlotStore();

        source.Entries.Add(new BookingSlot { ItemId = "a", ContentItemId = "tour", SlotKey = "2026-11-02", StartsUtc = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), Capacity = 12, Sold = 4, Price = 89.5m });
        source.Entries.Add(new BookingSlot { ItemId = "b", ContentItemId = "class", SlotKey = "2026-11-03T09:30", StartsUtc = new DateTime(2026, 11, 3, 9, 30, 0, DateTimeKind.Utc), Capacity = 6 });
        source.Entries.Add(new BookingSlot { ItemId = "c", ContentItemId = "tour", SlotKey = "2026-10-01", StartsUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), Capacity = 12 });
        source.Entries.Add(new BookingSlot { ItemId = "d", ContentItemId = "tour", SlotKey = "2026-11-09", StartsUtc = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc), Capacity = 12, Closed = true });

        var target = new InMemorySlotStore();
        var step = new BookingDatesStep(target, Capacity(target), Clock());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new BookingDatesDeploymentSource(source, Clock()), new BookingDatesDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Equal("BookingDates", exported["name"].GetValue<string>());
        Assert.Equal(2, exported["Dates"].AsArray().Count);
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(2, target.Entries.Count);

        var tour = target.Entries.Single(slot => slot.ContentItemId == "tour");
        var lesson = target.Entries.Single(slot => slot.ContentItemId == "class");

        Assert.Equal("2026-11-02", tour.SlotKey);
        Assert.Equal(12, tour.Capacity);
        Assert.Equal(0, tour.Sold);
        Assert.Equal(89.5m, tour.Price);
        Assert.Equal("2026-11-03T09:30", lesson.SlotKey);
        Assert.Equal(new DateTime(2026, 11, 3, 9, 30, 0, DateTimeKind.Utc), lesson.StartsUtc);
        Assert.Null(lesson.Price);
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();

        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static BookingCapacityService Capacity(IBookingSlotStore slots)
    {
        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(Mock.Of<ISite>());

        return new BookingCapacityService(slots, Mock.Of<IBookingStore>(), Mock.Of<ISession>(), Mock.Of<IDistributedLock>(), siteService.Object, Clock(), NullLogger<BookingCapacityService>.Instance);
    }

    private sealed class InMemorySlotStore : InMemoryCatalog<BookingSlot>, IBookingSlotStore
    {
        public Task<BookingSlot> FindAsync(string contentItemId, string slotKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(slot => slot.ContentItemId == contentItemId && slot.SlotKey == slotKey));

        public Task<IReadOnlyList<BookingSlot>> ListAsync(string contentItemId, IEnumerable<string> slotKeys, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BookingSlot>>(Entries.Where(slot => slot.ContentItemId == contentItemId && slotKeys.Contains(slot.SlotKey)).ToList());

        public Task<IReadOnlyList<BookingSlot>> ListAsync(string contentItemId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BookingSlot>>(Entries.Where(slot => (contentItemId is null || slot.ContentItemId == contentItemId) && slot.StartsUtc >= fromUtc && slot.StartsUtc < toUtc).ToList());
    }
}
