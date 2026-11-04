using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// The capacity planner: the agents the work needs, attrition and hiring classes that train and ramp.
/// </summary>
public sealed class CapacityPlannerTests
{
    private static readonly DateOnly _monday = new(2027, 1, 4);

    [Fact]
    public void TheAgentsNeeded_AllowForOccupancyAndShrinkage()
    {
        var plan = new CapacityPlan { StartDate = _monday, Weeks = 4, CurrentFte = 30, AttritionPercentPerMonth = 0, HoursPerFte = 40 };

        var weeks = CapacityPlanner.Project(plan, weeklyWorkloadHours: 600, shrinkagePercent: 25, maxOccupancyPercent: 80);

        // 600 handle hours at 80% occupancy is 750 hours on the phones; 40 hours less 25% is 30 a week each: 25 agents.
        Assert.All(weeks, week => Assert.Equal(25, week.RequiredFte, 6));
        Assert.All(weeks, week => Assert.Equal(5, week.Gap, 6));
    }

    [Fact]
    public void Attrition_TakesTheMonthsShareOfAgentsOverItsWeeks()
    {
        var plan = new CapacityPlan { StartDate = _monday, Weeks = 53, CurrentFte = 100, AttritionPercentPerMonth = 2 };

        var weeks = CapacityPlanner.Project(plan, 1000, 30, 85);

        // A year of 2% a month leaves 0.98^12 of the agents.
        Assert.Equal(100 * Math.Pow(0.98, 12), weeks[52].AvailableFte, 3);
    }

    [Fact]
    public void AHiringClass_TrainsThenRampsUp()
    {
        var plan = new CapacityPlan
        {
            StartDate = _monday,
            Weeks = 12,
            CurrentFte = 10,
            AttritionPercentPerMonth = 0,
            HiringClasses = [new HiringClass { StartDate = _monday.AddDays(14), Size = 8, TrainingWeeks = 2, RampWeeks = 4 }],
        };

        var weeks = CapacityPlanner.Project(plan, 300, 30, 85);

        Assert.Equal(10, weeks[1].AvailableFte, 6);
        Assert.Equal(8, weeks[2].InTrainingFte, 6);
        Assert.Equal(10, weeks[3].AvailableFte, 6);
        Assert.Equal(12, weeks[4].AvailableFte, 6);
        Assert.Equal(18, weeks[7].AvailableFte, 6);
        Assert.Equal(18, weeks[11].AvailableFte, 6);
    }

    [Fact]
    public void Growth_RaisesTheWorkOverTheYear()
    {
        var plan = new CapacityPlan { StartDate = _monday, Weeks = 53, CurrentFte = 10, GrowthPercentPerYear = 10 };

        var weeks = CapacityPlanner.Project(plan, 1000, 30, 85);

        Assert.Equal(1000, weeks[0].WorkloadHours, 6);
        Assert.Equal(1100, weeks[52].WorkloadHours, 6);
    }
}
