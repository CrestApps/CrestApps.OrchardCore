using CrestApps.OrchardCore.ServiceLevels.Core.Models;
using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Lays a policy's targets out as the editor's grid, a row per priority of the scope plus a last row for every other
/// priority and a column per metric, and reads the grid back. Pure.
/// </summary>
public static class ServiceLevelTargetGrid
{
    /// <summary>
    /// Builds the cells of the grid, row by row.
    /// </summary>
    /// <param name="targets">The policy's targets.</param>
    /// <param name="metrics">The scope's metrics.</param>
    /// <param name="priorities">The scope's priorities, without the "every other priority" row.</param>
    /// <returns>The cells: for each priority and then for every other priority, one cell per metric.</returns>
    public static IList<ServiceLevelTargetCell> BuildCells(
        IEnumerable<ServiceLevelTarget> targets,
        IEnumerable<ServiceLevelOption> metrics,
        IEnumerable<ServiceLevelOption> priorities)
    {
        var all = (targets ?? []).Where(target => target is not null).ToList();
        var metricKeys = (metrics ?? []).Select(metric => metric.Key).ToList();
        var cells = new List<ServiceLevelTargetCell>();

        foreach (var priority in Rows(priorities))
        {
            foreach (var metric in metricKeys)
            {
                var target = all.LastOrDefault(target => Same(target.Metric, metric) && Same(target.Priority, priority));

                cells.Add(new ServiceLevelTargetCell
                {
                    Metric = metric,
                    Priority = priority,
                    Minutes = target?.Minutes > 0 ? target.Minutes : null,
                });
            }
        }

        return cells;
    }

    /// <summary>
    /// Reads the grid back into targets. Cells with no minutes are dropped. Targets of a metric or priority the grid
    /// doesn't show (one the scope no longer offers) are kept as they were, so saving never loses them silently.
    /// </summary>
    /// <param name="existing">The policy's targets before the edit.</param>
    /// <param name="cells">The posted cells.</param>
    /// <param name="metrics">The scope's metrics.</param>
    /// <param name="priorities">The scope's priorities, without the "every other priority" row.</param>
    /// <returns>The targets.</returns>
    public static List<ServiceLevelTarget> ApplyCells(
        IEnumerable<ServiceLevelTarget> existing,
        IEnumerable<ServiceLevelTargetCell> cells,
        IEnumerable<ServiceLevelOption> metrics,
        IEnumerable<ServiceLevelOption> priorities)
    {
        var metricKeys = (metrics ?? []).Select(metric => metric.Key).ToList();
        var priorityKeys = Rows(priorities).ToList();

        bool Shown(string metric, string priority)
            => metricKeys.Any(key => Same(key, metric)) && priorityKeys.Any(key => Same(key, priority));

        var targets = (existing ?? [])
            .Where(target => target is not null && !Shown(target.Metric, target.Priority))
            .ToList();

        foreach (var cell in cells ?? [])
        {
            if (cell is null || cell.Minutes is not > 0 || !Shown(cell.Metric, cell.Priority))
            {
                continue;
            }

            targets.Add(new ServiceLevelTarget
            {
                Metric = cell.Metric,
                Priority = string.IsNullOrEmpty(cell.Priority) ? null : cell.Priority,
                Minutes = cell.Minutes.Value,
            });
        }

        return targets;
    }

    private static IEnumerable<string> Rows(IEnumerable<ServiceLevelOption> priorities)
    {
        foreach (var priority in priorities ?? [])
        {
            if (!string.IsNullOrEmpty(priority?.Key))
            {
                yield return priority.Key;
            }
        }

        yield return string.Empty;
    }

    private static bool Same(string left, string right)
        => string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
