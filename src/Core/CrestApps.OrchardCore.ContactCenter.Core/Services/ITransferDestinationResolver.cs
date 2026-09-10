using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Resolves typed, logical transfer destinations into provider-safe endpoints.
/// </summary>
public interface ITransferDestinationResolver
{
    /// <summary>
    /// Resolves and authorizes a transfer destination.
    /// </summary>
    /// <param name="request">The transfer request containing a typed logical destination.</param>
    /// <param name="principal">The authenticated principal initiating the transfer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The resolved destination result.</returns>
    Task<TransferDestinationResolutionResult> ResolveAsync(
        TransferRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
