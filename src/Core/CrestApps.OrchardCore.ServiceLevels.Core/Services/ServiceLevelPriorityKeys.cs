using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Keys a policy's targets by the priority keys its scope gives (<see cref="IServiceLevelScopeProvider.GetPrioritiesAsync"/>),
/// so a target written with a priority's name (as a recipe that can't know a site's ids writes it) follows the priority
/// when it is renamed.
/// </summary>
public static class ServiceLevelPriorityKeys
{
    /// <summary>
    /// Rewrites each target whose priority is a priority's name, not its key, to that key.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <param name="scope">The policy's scope, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether a target changed.</returns>
    public static async Task<bool> KeyByIdAsync(ServiceLevelPolicy policy, IServiceLevelScopeProvider scope, CancellationToken cancellationToken = default)
    {
        if (policy?.Targets is not { Count: > 0 } || scope is null)
        {
            return false;
        }

        var priorities = await scope.GetPrioritiesAsync(cancellationToken) ?? [];
        var changed = false;

        foreach (var target in policy.Targets.Where(target => !string.IsNullOrWhiteSpace(target.Priority)))
        {
            if (priorities.Any(priority => string.Equals(priority.Key, target.Priority, StringComparison.Ordinal)))
            {
                continue;
            }

            if (priorities.FirstOrDefault(priority => string.Equals(priority.Name?.Trim(), target.Priority.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(priority.Key, target.Priority.Trim(), StringComparison.OrdinalIgnoreCase)) is { } named)
            {
                target.Priority = named.Key;
                changed = true;
            }
        }

        return changed;
    }
}
