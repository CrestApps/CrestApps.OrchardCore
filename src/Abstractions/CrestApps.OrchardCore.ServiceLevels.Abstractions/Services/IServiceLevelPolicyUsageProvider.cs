using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Services;

/// <summary>
/// Says which of a module's records use a service level policy, such as the work order types or ticket desks that name
/// it, so the policy isn't deleted while they do. Register one with
/// <see cref="ServiceLevelsServiceCollectionExtensions.AddServiceLevelPolicyUsage{TProvider}"/>.
/// </summary>
public interface IServiceLevelPolicyUsageProvider
{
    /// <summary>
    /// Lists the records that use a policy.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A name for each record that uses it, such as "Work order type: Boiler service"; empty when none does.</returns>
    Task<IReadOnlyList<string>> GetUsagesAsync(ServiceLevelPolicy policy, CancellationToken cancellationToken = default);
}
