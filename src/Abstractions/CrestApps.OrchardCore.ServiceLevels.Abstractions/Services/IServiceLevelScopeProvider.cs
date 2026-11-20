using CrestApps.OrchardCore.ServiceLevels.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ServiceLevels.Services;

/// <summary>
/// Describes one kind of work that service level policies are written for, such as work orders or tickets: the metrics
/// it measures and the priorities its records have. The module that owns the work registers one with
/// <see cref="ServiceLevelsServiceCollectionExtensions.AddServiceLevelScope{TProvider}"/>; the policy editor then offers
/// that scope with a target per metric and priority.
/// </summary>
public interface IServiceLevelScopeProvider
{
    /// <summary>
    /// Gets the scope's name. It is stored in every policy of the scope, so it never changes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name of the scope shown to users.
    /// </summary>
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets what the scope's policies are for, shown when choosing the kind of a new policy; <see langword="null"/> for none.
    /// </summary>
    LocalizationSource Description => null;

    /// <summary>
    /// Gets the metrics the scope measures, in display order.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The metrics.</returns>
    Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the priorities the scope's records can have, most urgent first.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The priorities.</returns>
    Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default);
}
