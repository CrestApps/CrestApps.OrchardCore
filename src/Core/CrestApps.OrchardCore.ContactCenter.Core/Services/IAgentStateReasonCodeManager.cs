using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for agent state reason codes.
/// </summary>
public interface IAgentStateReasonCodeManager : ICatalogManager<AgentStateReasonCode>
{
    /// <summary>
    /// Finds the reason code with the specified unique name.
    /// </summary>
    /// <param name="name">The reason code name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching reason code, or <see langword="null"/> when none exists.</returns>
    Task<AgentStateReasonCode> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every enabled reason code ordered for display.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled reason codes.</returns>
    Task<IReadOnlyCollection<AgentStateReasonCode>> ListEnabledAsync(CancellationToken cancellationToken = default);
}
