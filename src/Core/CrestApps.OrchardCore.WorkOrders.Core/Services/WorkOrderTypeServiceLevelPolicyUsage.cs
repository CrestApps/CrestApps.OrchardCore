using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Keeps a service level policy from being deleted while a work order type names it.
/// </summary>
public sealed class WorkOrderTypeServiceLevelPolicyUsage : IServiceLevelPolicyUsageProvider
{
    private readonly WorkOrderSettingManager<WorkOrderType> _types;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderTypeServiceLevelPolicyUsage"/> class.
    /// </summary>
    /// <param name="types">The work order types.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderTypeServiceLevelPolicyUsage(
        WorkOrderSettingManager<WorkOrderType> types,
        IStringLocalizer<WorkOrderTypeServiceLevelPolicyUsage> stringLocalizer)
    {
        _types = types;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetUsagesAsync(ServiceLevelPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var types = await _types.GetAllAsync(cancellationToken);

        return [.. types
            .Where(type => string.Equals(type.ServiceLevelPolicyId, policy.ItemId, StringComparison.Ordinal))
            .Select(type => S["Work order type: {0}", type.Name].Value)];
    }
}
