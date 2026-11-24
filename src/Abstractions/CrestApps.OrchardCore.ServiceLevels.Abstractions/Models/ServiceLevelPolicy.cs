using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Models;

/// <summary>
/// A service level policy: how long each metric (first response, resolution...) may take at each priority, counted on
/// a business-hours schedule or around the clock. A policy belongs to one scope, the kind of work it is for (work
/// orders, tickets), which names its metrics and priorities. Other modules keep their own settings for a policy in its
/// <c>Properties</c>.
/// </summary>
public sealed class ServiceLevelPolicy : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a description for administrators.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the scope the policy is for: the name of a registered
    /// <see cref="Services.IServiceLevelScopeProvider"/>.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets the targets.
    /// </summary>
    public List<ServiceLevelTarget> Targets { get; set; } = [];

    /// <summary>
    /// Gets or sets the Business Hours schedule the targets are counted on. Empty counts around the clock.
    /// </summary>
    public string HoursScheduleId { get; set; }

    /// <summary>
    /// Gets or sets the share of a target, from 1 to 99, after which a timer warns.
    /// </summary>
    public int WarningPercent { get; set; } = ServiceLevelsConstants.DefaultWarningPercent;

    /// <summary>
    /// Gets or sets when the policy was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the policy was last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Finds the target of a metric at a priority: the target set for that priority, otherwise the metric's target for
    /// every priority.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <param name="priority">The priority key, or empty.</param>
    /// <returns>The target, or <see langword="null"/> when the policy sets none (or one of zero minutes).</returns>
    public ServiceLevelTarget FindTarget(string metric, string priority)
    {
        ServiceLevelTarget fallback = null;

        foreach (var target in Targets ?? [])
        {
            if (target is null || target.Minutes <= 0 || !string.Equals(target.Metric, metric, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(target.Priority))
            {
                fallback ??= target;
            }
            else if (string.Equals(target.Priority, priority, StringComparison.OrdinalIgnoreCase))
            {
                return target;
            }
        }

        return fallback;
    }
}
