using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Recipes;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Scheduling.Core;
using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Recipes;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// The Scheduling and Calendars recipe steps create records by id, update them in place when run again, and read enums by
/// name, on a real SQLite store.
/// </summary>
public sealed class SchedulingRecipeTests
{
    [Fact]
    public async Task SchedulingStep_CreatesThenUpdatesInPlace()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var step = JsonNode.Parse("""
            {
              "name": "Scheduling",
              "Resources": [
                { "itemId": "res-1", "name": "Dr. Smith", "kind": "Staff", "timeZoneId": "America/New_York",
                  "workingHours": [ { "day": "Monday", "start": "09:00", "end": "17:00" } ], "enabled": true }
              ],
              "AppointmentTypes": [
                { "itemId": "type-1", "name": "Consultation", "slug": "Consultation", "durationMinutes": 30, "slotIntervalMinutes": 30,
                  "resourceIds": [ "res-1" ], "assignment": "RoundRobin", "allowSelfService": true, "enabled": true }
              ],
              "Appointments": [
                { "itemId": "apt-1", "appointmentTypeId": "type-1", "resourceIds": [ "res-1" ], "startUtc": "2026-11-03T15:00:00Z",
                  "endUtc": "2026-11-03T15:30:00Z", "status": "Confirmed", "customerName": "Ada Lovelace" }
              ],
              "Waitlist": [
                { "itemId": "wait-1", "appointmentTypeId": "type-1", "fromUtc": "2026-11-03T00:00:00Z", "toUtc": "2026-11-04T00:00:00Z",
                  "customerName": "Grace Hopper", "status": "Waiting" }
              ]
            }
            """).AsObject();

        // Act
        var firstErrors = await RunAsync(harness, step);

        step["AppointmentTypes"][0]["name"] = "Long consultation";
        step["AppointmentTypes"][0]["durationMinutes"] = 60;

        var secondErrors = await RunAsync(harness, step);

        // Assert
        Assert.Empty(firstErrors);
        Assert.Empty(secondErrors);

        await harness.RunAsync(async services =>
        {
            var types = await services.GetRequiredService<AppointmentTypeStore>().ListAsync();
            var resources = await services.GetRequiredService<SchedulingResourceStore>().ListAsync();
            var appointment = await services.GetRequiredService<AppointmentStore>().FindByIdAsync("apt-1");
            var (_, waitlist) = await services.GetRequiredService<WaitlistStore>().PageAsync(0, 10);

            var type = Assert.Single(types);

            Assert.Equal("Long consultation", type.Name);
            Assert.Equal(60, type.DurationMinutes);
            Assert.Equal("consultation", type.Slug);
            Assert.Equal(AppointmentAssignment.RoundRobin, type.Assignment);
            Assert.Equal(SchedulingResourceKind.Staff, Assert.Single(resources).Kind);
            Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
            Assert.False(string.IsNullOrEmpty(appointment.ManageToken));
            Assert.Equal("Grace Hopper", Assert.Single(waitlist).CustomerName);
        }, commit: false);
    }

    [Fact]
    public async Task CalendarsStep_ImportsCategories()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var step = JsonNode.Parse("""
            {
              "name": "Calendars",
              "Categories": [
                { "itemId": "cat-1", "name": "Holidays", "color": "#e03131" },
                { "itemId": "cat-2", "name": "Sales calls", "color": "#2f9e44", "ownerId": "ada" }
              ],
              "Calendars": []
            }
            """).AsObject();

        // Act
        var errors = await harness.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = CalendarsConstants.RecipeStepName, Step = step };

            await ActivatorUtilities.CreateInstance<CalendarsRecipeStep>(services).ExecuteAsync(context);

            return context.Errors;
        });

        var forAda = await harness.RunAsync(services => services.GetRequiredService<ICalendarCategoryService>().ListAsync("ada", TestContext.Current.CancellationToken), commit: false);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(["Holidays", "Sales calls"], forAda.Select(category => category.Name));
        Assert.True(forAda[0].IsShared);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return CalendarTestHarness.CreateAsync(
            services => services.AddSchedulingCore(),
            SchedulingIndexSchema.CreateAsync,
            [new SchedulingResourceIndexProvider(), new AppointmentTypeIndexProvider(), new AppointmentIndexProvider(), new WaitlistEntryIndexProvider()],
            [SchedulingConstants.CollectionName]);
    }

    private static Task<IList<string>> RunAsync(CalendarTestHarness harness, JsonObject step)
    {
        return harness.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = SchedulingConstants.RecipeStepName, Step = step.DeepClone().AsObject() };

            await ActivatorUtilities.CreateInstance<SchedulingRecipeStep>(services).ExecuteAsync(context);

            return context.Errors;
        });
    }
}
