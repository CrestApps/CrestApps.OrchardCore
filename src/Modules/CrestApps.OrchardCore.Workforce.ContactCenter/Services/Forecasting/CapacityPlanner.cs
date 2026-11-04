using CrestApps.OrchardCore.Workforce.ContactCenter.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;

/// <summary>
/// Projects a capacity plan week by week. Pure.
/// </summary>
/// <remarks>
/// The work grows evenly over the year. The agents it needs are its handle hours, over the highest occupancy, over a
/// full-time agent's hours less shrinkage. The agents expected lose the month's attrition spread over its weeks, and
/// gain each hiring class after its training, rising evenly to full productivity over its ramp.
/// </remarks>
public static class CapacityPlanner
{
    /// <summary>
    /// The most weeks a plan covers.
    /// </summary>
    public const int MaxWeeks = 78;

    /// <summary>
    /// Projects the plan.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="weeklyWorkloadHours">The handle hours of a week of work at the start.</param>
    /// <param name="shrinkagePercent">The planning group's shrinkage.</param>
    /// <param name="maxOccupancyPercent">The planning group's highest occupancy.</param>
    /// <returns>The weeks, in order.</returns>
    public static List<CapacityWeek> Project(CapacityPlan plan, double weeklyWorkloadHours, double shrinkagePercent, double maxOccupancyPercent)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var weeks = new List<CapacityWeek>();
        var weeklyRetention = Math.Pow(1 - (Math.Clamp(plan.AttritionPercentPerMonth, 0, 100) / 100d), 12d / 52d);
        var productiveHours = Math.Max(0.1, plan.HoursPerFte * (1 - (Math.Clamp(shrinkagePercent, 0, 99) / 100d)));
        var occupancy = Math.Clamp(maxOccupancyPercent, 1, 100) / 100d;
        var staff = Math.Max(0, plan.CurrentFte);
        var classes = plan.HiringClasses.Select(item => (Class: item, Remaining: item.Size)).ToList();

        for (var week = 0; week < Math.Clamp(plan.Weeks, 1, MaxWeeks); week++)
        {
            var weekStart = plan.StartDate.AddDays(7 * week);
            var workload = weeklyWorkloadHours * Math.Pow(1 + (plan.GrowthPercentPerYear / 100d), week / 52d);

            if (week > 0)
            {
                staff *= weeklyRetention;
            }

            var available = staff;
            var training = 0d;

            for (var i = 0; i < classes.Count; i++)
            {
                var (hiring, remaining) = classes[i];

                if (weekStart < hiring.StartDate)
                {
                    continue;
                }

                // A class loses attrition like everyone else from its first week.
                if (weekStart > hiring.StartDate)
                {
                    remaining *= weeklyRetention;
                    classes[i] = (hiring, remaining);
                }

                var weeksIn = (weekStart.DayNumber - hiring.StartDate.DayNumber) / 7;

                if (weeksIn < hiring.TrainingWeeks)
                {
                    training += remaining;
                    continue;
                }

                var ramp = Math.Max(1, hiring.RampWeeks);
                var productivity = Math.Min(1, (weeksIn - hiring.TrainingWeeks + 1) / (double)ramp);

                available += remaining * productivity;
            }

            weeks.Add(new CapacityWeek
            {
                WeekStart = weekStart,
                WorkloadHours = workload,
                RequiredFte = workload / occupancy / productiveHours,
                AvailableFte = available,
                InTrainingFte = training,
            });
        }

        return weeks;
    }
}
