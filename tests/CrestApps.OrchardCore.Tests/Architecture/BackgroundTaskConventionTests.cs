using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Every background task runs once per tenant on every node, so how often it runs and how it holds its lock decide the
/// load a site carries (see CQ-5 in <c>docs/engineering/review-2026-11/04-code-quality.md</c>).
/// <para>
/// A task states <c>LockTimeout</c> and <c>LockExpiration</c>, so a second node waits a known time and a crashed node's
/// lock expires when the work would have ended. The number of tasks that run every minute only goes down: each one opens
/// a shell scope per tenant per minute.
/// </para>
/// </summary>
public sealed partial class BackgroundTaskConventionTests
{
    /// <summary>
    /// The number of tasks scheduled every minute when the ratchet was set. Lower it when one moves to a slower
    /// schedule or merges into another; never raise it.
    /// </summary>
    private const int KnownEveryMinuteTasks = 45;

    private static readonly Lazy<List<BackgroundTaskDeclaration>> _tasks = new(ScanSources);

    [Fact]
    public void EveryBackgroundTask_StatesItsLockTimeoutAndExpiration()
    {
        // Arrange
        var missing = _tasks.Value
            .Where(task => !task.HasLockTimeout || !task.HasLockExpiration)
            .Select(task => $"{task.RelativePath}: {task.Name} sets no {(task.HasLockTimeout ? "LockExpiration" : task.HasLockExpiration ? "LockTimeout" : "LockTimeout or LockExpiration")}")
            .ToList();

        // Assert
        Assert.True(_tasks.Value.Count > 50, $"Only {_tasks.Value.Count} background tasks were found; the scan no longer matches the source.");
        Assert.True(missing.Count == 0, Ratchet.BuildMessage(
            "These background tasks leave their lock to the defaults. Set LockTimeout (how long another node waits) and LockExpiration (when a crashed node's lock is released) on the [BackgroundTask] attribute:",
            missing));
    }

    [Fact]
    public void EveryMinuteSchedules_OnlyGoDown()
    {
        // Arrange
        var everyMinute = _tasks.Value.Where(task => task.RunsEveryMinute).Select(task => $"{task.RelativePath}: {task.Name}").ToList();

        // Assert
        Assert.True(everyMinute.Count <= KnownEveryMinuteTasks, Ratchet.BuildMessage(
            $"{everyMinute.Count} background tasks run every minute, more than the {KnownEveryMinuteTasks} recorded. Run the new one less often, or merge it into a task that already runs:",
            everyMinute));
        Assert.True(everyMinute.Count >= KnownEveryMinuteTasks,
            $"{everyMinute.Count} background tasks run every minute, fewer than the {KnownEveryMinuteTasks} recorded. Lower {nameof(KnownEveryMinuteTasks)} to {everyMinute.Count} so the gain is kept.");
    }

    [Theory]
    [InlineData("[BackgroundTask(Title = \"A\", Schedule = \"* * * * *\", LockTimeout = 3_000, LockExpiration = 30_000)]\npublic sealed class ATask : IBackgroundTask", true, true, true)]
    [InlineData("[BackgroundTask(\n    Title = \"B\",\n    Schedule = \"*/5 * * * *\",\n    Description = \"Runs (sometimes).\")]\ninternal sealed class BTask : IBackgroundTask", false, false, false)]
    [InlineData("[BackgroundTask(Schedule = \"* * * * *\", LockTimeout = 1_000)]\npublic class CTask : IBackgroundTask", true, false, true)]
    public void Read_FindsTheScheduleAndTheLockSettings(string source, bool hasLockTimeout, bool hasLockExpiration, bool everyMinute)
    {
        // Act
        var task = Assert.Single(Read("test.cs", source));

        // Assert
        Assert.Equal(hasLockTimeout, task.HasLockTimeout);
        Assert.Equal(hasLockExpiration, task.HasLockExpiration);
        Assert.Equal(everyMinute, task.RunsEveryMinute);
    }

    private static List<BackgroundTaskDeclaration> ScanSources()
    {
        var tasks = new List<BackgroundTaskDeclaration>();

        foreach (var file in Ratchet.EnumerateFiles("src"))
        {
            var source = Ratchet.ReadAllText(file);

            if (source.Contains("[BackgroundTask", StringComparison.Ordinal))
            {
                tasks.AddRange(Read(file.RelativePath, source));
            }
        }

        return tasks;
    }

    private static IEnumerable<BackgroundTaskDeclaration> Read(string relativePath, string source)
    {
        var code = CSharpSourceMask.Comments(source);
        var masked = CSharpSourceMask.CommentsAndLiterals(source);

        foreach (Match attribute in BackgroundTaskAttribute().Matches(masked))
        {
            var open = attribute.Index + attribute.Length - 1;
            var close = CSharpSourceMask.FindClosing(masked, open);
            var arguments = masked[(open + 1)..close];
            var schedule = ScheduleArgument().Match(code, open, close - open);
            var name = ClassName().Match(masked, close);

            yield return new BackgroundTaskDeclaration(
                relativePath,
                name.Success ? name.Groups["name"].Value : "?",
                LockTimeout().IsMatch(arguments),
                LockExpiration().IsMatch(arguments),
                schedule.Success && EveryMinute().IsMatch(schedule.Groups["schedule"].Value));
        }
    }

    [GeneratedRegex(@"\[\s*BackgroundTask\s*\(")]
    private static partial Regex BackgroundTaskAttribute();

    [GeneratedRegex(@"\bSchedule\s*=\s*""(?<schedule>[^""]*)""")]
    private static partial Regex ScheduleArgument();

    [GeneratedRegex(@"^\s*\*\s+\*\s+\*\s+\*\s+\*\s*$")]
    private static partial Regex EveryMinute();

    [GeneratedRegex(@"\bLockTimeout\s*=")]
    private static partial Regex LockTimeout();

    [GeneratedRegex(@"\bLockExpiration\s*=")]
    private static partial Regex LockExpiration();

    [GeneratedRegex(@"\bclass\s+(?<name>\w+)")]
    private static partial Regex ClassName();

    private sealed record BackgroundTaskDeclaration(string RelativePath, string Name, bool HasLockTimeout, bool HasLockExpiration, bool RunsEveryMinute);
}
