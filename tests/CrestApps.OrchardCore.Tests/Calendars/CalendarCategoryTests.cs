using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// Each user sees their own categories and the shared ones, never someone else's, on a real SQLite store.
/// </summary>
public sealed class CalendarCategoryTests
{
    [Fact]
    public async Task List_GivesTheUsersOwnAndSharedCategories_ByName()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();

        await SaveAsync(harness, new CalendarCategory { Name = "Sales calls", Color = "#2f9e44", OwnerId = "ada" });
        await SaveAsync(harness, new CalendarCategory { Name = "Holidays", Color = "#e03131" });
        await SaveAsync(harness, new CalendarCategory { Name = "Grace's gym", Color = "#1971c2", OwnerId = "grace" });

        // Act
        var forAda = await ListAsync(harness, "ada");
        var sharedOnly = await ListAsync(harness, null);

        // Assert
        Assert.Equal(["Holidays", "Sales calls"], forAda.Select(category => category.Name));
        Assert.Equal(["Holidays"], sharedOnly.Select(category => category.Name));
        Assert.True(sharedOnly[0].IsShared);
    }

    [Fact]
    public async Task GetAndDelete_WorkByIds()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var kept = await SaveAsync(harness, new CalendarCategory { Name = "Kept", Color = "#2f9e44", OwnerId = "ada" });
        var deleted = await SaveAsync(harness, new CalendarCategory { Name = "Deleted", Color = "#e03131", OwnerId = "ada" });

        // Act
        await harness.RunAsync(async services =>
        {
            var categories = services.GetRequiredService<ICalendarCategoryService>();

            await categories.DeleteAsync(await categories.FindByIdAsync(deleted.ItemId, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        });

        var found = await harness.RunAsync(
            services => services.GetRequiredService<ICalendarCategoryService>().GetAsync([kept.ItemId, deleted.ItemId, null], TestContext.Current.CancellationToken),
            commit: false);

        // Assert
        Assert.Equal(["Kept"], found.Select(category => category.Name));
        Assert.Equal(CalendarTestHarness.Now, found[0].CreatedUtc);
    }

    private static Task<CalendarCategory> SaveAsync(CalendarTestHarness harness, CalendarCategory category)
    {
        return harness.RunAsync(async services =>
        {
            await services.GetRequiredService<ICalendarCategoryService>().SaveAsync(category, TestContext.Current.CancellationToken);

            return category;
        });
    }

    private static Task<IReadOnlyList<CalendarCategory>> ListAsync(CalendarTestHarness harness, string userId)
    {
        return harness.RunAsync(
            services => services.GetRequiredService<ICalendarCategoryService>().ListAsync(userId, TestContext.Current.CancellationToken),
            commit: false);
    }
}
