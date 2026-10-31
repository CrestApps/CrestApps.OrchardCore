using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Adds the usual activity codes the first time the feature runs, when there are none yet.
/// </summary>
internal sealed class ActivityCodeSeeder
{
    private readonly ActivityCodeStore _store;

    public ActivityCodeSeeder(ActivityCodeStore store)
    {
        _store = store;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _store.CountAsync(cancellationToken) > 0)
        {
            return;
        }

        foreach (var code in Defaults())
        {
            await _store.CreateAsync(code, cancellationToken);
        }
    }

    private static IEnumerable<ActivityCode> Defaults()
    {
        yield return new ActivityCode { Name = "Work", Code = "WRK", Kind = ActivityKind.Work, Color = "#2b8a3e" };
        yield return new ActivityCode { Name = "Break", Code = "BRK", Kind = ActivityKind.Break, Color = "#f59f00", CountsTowardCoverage = false };
        yield return new ActivityCode { Name = "Meal", Code = "MEL", Kind = ActivityKind.Meal, Color = "#e67700", IsPaid = false, CountsAsWork = false, CountsTowardCoverage = false };
        yield return new ActivityCode { Name = "Meeting", Code = "MTG", Kind = ActivityKind.Meeting, Color = "#6741d9", CountsTowardCoverage = false };
        yield return new ActivityCode { Name = "Training", Code = "TRN", Kind = ActivityKind.Training, Color = "#1971c2", CountsTowardCoverage = false };
        yield return new ActivityCode { Name = "Coaching", Code = "CCH", Kind = ActivityKind.Coaching, Color = "#0c8599", CountsTowardCoverage = false };
        yield return new ActivityCode { Name = "Offline work", Code = "OFF", Kind = ActivityKind.OfflineWork, Color = "#868e96", CountsTowardCoverage = false };
        yield return new ActivityCode { Name = "Travel", Code = "TRV", Kind = ActivityKind.Travel, Color = "#5c940d" };
        yield return new ActivityCode { Name = "On call", Code = "ONC", Kind = ActivityKind.OnCall, Color = "#f08c00", CountsAsWork = false, CountsTowardCoverage = false };
    }
}
