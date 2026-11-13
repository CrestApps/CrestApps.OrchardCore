using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Makes work orders a kind of work that service level policies are written for: a work order is measured on its
/// first response and its resolution, at its priority.
/// </summary>
public sealed class WorkOrderServiceLevelScope : IServiceLevelScopeProvider
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<WorkOrderServiceLevelScope>("Work orders");

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderServiceLevelScope"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderServiceLevelScope(IStringLocalizer<WorkOrderServiceLevelScope> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => WorkOrdersConstants.ServiceLevelScope;

    /// <inheritdoc/>
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServiceLevelOption> metrics =
        [
            new(ServiceLevelMetrics.FirstResponse, S["Response"]),
            new(ServiceLevelMetrics.Resolution, S["Resolution"]),
        ];

        return Task.FromResult(metrics);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServiceLevelOption> priorities =
        [
            new(nameof(WorkOrderPriority.Urgent), S["Urgent"]),
            new(nameof(WorkOrderPriority.High), S["High"]),
            new(nameof(WorkOrderPriority.Normal), S["Normal"]),
            new(nameof(WorkOrderPriority.Low), S["Low"]),
        ];

        return Task.FromResult(priorities);
    }
}
