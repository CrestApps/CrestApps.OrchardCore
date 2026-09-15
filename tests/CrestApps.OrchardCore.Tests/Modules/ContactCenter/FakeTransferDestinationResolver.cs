using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A configurable transfer destination resolver that records the requests it resolved.
/// </summary>
public sealed class FakeTransferDestinationResolver : ITransferDestinationResolver
{
    private readonly Func<TransferRequest, TransferDestinationResolutionResult> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTransferDestinationResolver"/> class.
    /// </summary>
    /// <param name="handler">The resolution to apply, or <see langword="null"/> to echo the requested destination.</param>
    public FakeTransferDestinationResolver(
        Func<TransferRequest, TransferDestinationResolutionResult> handler = null)
    {
        _handler = handler ?? (request => TransferDestinationResolutionResult.Success(request.TargetType, request.TargetId));
    }

    /// <summary>
    /// Gets every request the resolver was asked to resolve, in call order.
    /// </summary>
    public List<TransferRequest> Requests { get; } = [];

    /// <inheritdoc/>
    public Task<TransferDestinationResolutionResult> ResolveAsync(
        TransferRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        return Task.FromResult(_handler(request));
    }
}
