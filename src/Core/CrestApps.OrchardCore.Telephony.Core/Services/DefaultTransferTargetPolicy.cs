using System.Security.Claims;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// The default <see cref="ITransferTargetPolicy"/> for a deployment with no curated destination catalog: the
/// agent's target is handed to the provider as typed, once <see cref="IDialDestinationPolicy"/> has confirmed it
/// is not a destination the platform refuses. A target the destination policy cannot parse is still allowed,
/// because a transfer target may be a provider directory address rather than a dialable number.
/// </summary>
public sealed class DefaultTransferTargetPolicy : ITransferTargetPolicy
{
    private readonly IDialDestinationPolicy _destinationPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTransferTargetPolicy"/> class.
    /// </summary>
    /// <param name="destinationPolicy">The safety policy deciding which destinations may be reached.</param>
    public DefaultTransferTargetPolicy(IDialDestinationPolicy destinationPolicy)
    {
        _destinationPolicy = destinationPolicy;
    }

    /// <inheritdoc/>
    public Task<TransferTargetDecision> ResolveAsync(string rawTarget, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return Task.FromResult(TransferTargetDecision.Refuse("A transfer destination is required."));
        }

        var target = rawTarget.Trim();
        var decision = _destinationPolicy.Evaluate(target, new DialDestinationContext
        {
            Operation = DialDestinationOperation.Transfer,
        });

        if (decision.Outcome is DialDestinationOutcome.Emergency
            or DialDestinationOutcome.Premium
            or DialDestinationOutcome.Blocked)
        {
            return Task.FromResult(TransferTargetDecision.Refuse(decision.Reason));
        }

        return Task.FromResult(TransferTargetDecision.Allow(target));
    }

    /// <inheritdoc/>
    public Task<TransferTargetDecision> ResolveAsync(TransferRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (request?.IsExtension != true)
        {
            return ResolveAsync(request?.To, user, cancellationToken);
        }

        // An extension is a colleague inside the phone system, reached through their own endpoint; the telephony
        // service resolves it to the user it rings. Only digits are an extension, so marking a number or an address
        // as one is not a way to reach it, and a refused code such as an emergency number stays refused.
        var extension = request.GetExtension();

        if (extension is null)
        {
            return Task.FromResult(TransferTargetDecision.Refuse("Enter the extension as digits only."));
        }

        return ResolveAsync(extension, user, cancellationToken);
    }
}
