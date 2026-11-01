using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// Capacity plans read the work at the start from the last four weeks when the plan doesn't set it.
/// </summary>
public sealed class CapacityPlanServiceTests
{
    [Fact]
    public async Task TheWorkAtTheStart_ComesFromTheLastFourWeeks()
    {
        var reader = new Mock<IIntervalStatisticsReader>();

        reader.Setup(r => r.GetQueueIntervalsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> _, DateTime fromUtc, DateTime toUtc, CancellationToken _) => History(fromUtc, toUtc));

        await using var harness = await WorkforceTests.CreateAsync(
            services =>
            {
                services.AddSingleton(reader.Object);
                services.AddScoped<PlanningGroupStore>();
                services.AddScoped<PlanningGroupManager>();
                services.AddScoped<ICatalogEntryHandler<PlanningGroup>, PlanningGroupHandler>();
                services.AddScoped<ForecastEventStore>();
                services.AddScoped<ForecastEventManager>();
                services.AddScoped<ForecastService>();
                services.AddScoped<CapacityPlanService>();
            },
            ForecastingIndexSchema.CreateAsync,
            [new PlanningGroupIndexProvider(), new ForecastEventIndexProvider(), new ForecastIndexProvider()]);

        var team = await WorkforceTests.AddTeamAsync(harness, "Sales", TeamKind.Team);

        await PlanningKit.AddMemberAsync(harness, "Ana", team);
        await PlanningKit.AddMemberAsync(harness, "Ben", team);

        var projection = await harness.RunAsync(async services =>
        {
            var groups = services.GetRequiredService<PlanningGroupManager>();
            var group = await groups.NewAsync();

            group.Name = "Sales voice";
            group.QueueIds = ["sales"];
            group.TeamId = team.ItemId;
            group.ShrinkagePercent = 25;
            group.MaxOccupancyPercent = 80;
            await groups.CreateAsync(group);

            var service = services.GetRequiredService<CapacityPlanService>();

            Assert.Equal(2, await service.TeamHeadcountAsync(group));

            return await service.ProjectAsync(new CapacityPlan
            {
                PlanningGroupId = group.ItemId,
                StartDate = new DateOnly(2026, 11, 9),
                Weeks = 2,
                HoursPerFte = 40,
                CurrentFte = 2,
                AttritionPercentPerMonth = 0,
            });
        });

        // 20 weekdays of 30 contacts at 6 minutes: 60 hours over four weeks, 15 a week.
        Assert.True(projection.WorkloadFromHistory);
        Assert.Equal(15, projection.WorkloadHours.Value, 6);

        // 15 hours at 80% occupancy is 18.75 on the phones; 40 hours less 25% is 30 each.
        Assert.Equal(0.625, projection.Weeks[0].RequiredFte, 6);
        Assert.Null(projection.WorstWeek);
    }

    private static List<QueueIntervalStatistic> History(DateTime fromUtc, DateTime toUtc)
    {
        var intervals = new List<QueueIntervalStatistic>();

        for (var day = fromUtc.Date; day < toUtc; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var start = DateTime.SpecifyKind(day.AddHours(15), DateTimeKind.Utc);

            if (start >= fromUtc && start < toUtc)
            {
                intervals.Add(new QueueIntervalStatistic { QueueId = "sales", IntervalStartUtc = start, Offered = 30, Handled = 30, TalkSeconds = 30 * 360 });
            }
        }

        return intervals;
    }
}
