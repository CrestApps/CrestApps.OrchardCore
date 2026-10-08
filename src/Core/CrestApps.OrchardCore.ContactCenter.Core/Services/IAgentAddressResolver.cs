using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Finds the numbers an agent calls and texts from.
/// </summary>
public interface IAgentAddressResolver
{
    /// <summary>
    /// Resolves the agent's numbers: the phone number whose line names them and the messaging number whose line names
    /// them, each falling back to the tenant's default number when they have none of their own.
    /// </summary>
    /// <param name="userId">The agent's user identifier, or <see langword="null"/> for the defaults alone.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The numbers; either may be <see langword="null"/> when there is no number and no default.</returns>
    Task<AgentAddresses> ResolveAsync(string userId, CancellationToken cancellationToken = default);
}
