using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Keeps a service level policy from being deleted while timers still count on it: their targets and hours come from it.
/// </summary>
public sealed class ActiveTimerPolicyUsage : IServiceLevelPolicyUsageProvider
{
    private readonly IServiceLevelTimerStore _timers;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveTimerPolicyUsage"/> class.
    /// </summary>
    /// <param name="timers">The timer store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActiveTimerPolicyUsage(IServiceLevelTimerStore timers, IStringLocalizer<ActiveTimerPolicyUsage> stringLocalizer)
    {
        _timers = timers;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetUsagesAsync(ServiceLevelPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return await _timers.AnyActiveAsync(policy.ItemId, cancellationToken)
            ? [S["Timers that are still running or paused"].Value]
            : [];
    }
}
