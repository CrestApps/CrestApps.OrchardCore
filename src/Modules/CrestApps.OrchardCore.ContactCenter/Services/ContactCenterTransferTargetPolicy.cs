using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The transfer target policy used when Contact Center Voice is enabled: an agent may only transfer to a
/// destination the tenant curates. What the agent typed is treated as an identifier and resolved through
/// <see cref="ITransferDestinationResolver"/> as an approved external destination, an agent, or a queue. A raw
/// phone number matches none of those and is refused, which is what closes the transfer-field bypass.
/// </summary>
public sealed class ContactCenterTransferTargetPolicy : ITransferTargetPolicy
{
    private static readonly InteractionTransferTargetType[] _candidateTargetTypes =
    [
        InteractionTransferTargetType.External,
        InteractionTransferTargetType.Agent,
        InteractionTransferTargetType.Queue,
    ];

    private readonly ITransferDestinationResolver _destinationResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTransferTargetPolicy"/> class.
    /// </summary>
    /// <param name="destinationResolver">The resolver that authorizes and resolves curated destinations.</param>
    public ContactCenterTransferTargetPolicy(ITransferDestinationResolver destinationResolver)
    {
        _destinationResolver = destinationResolver;
    }

    /// <inheritdoc/>
    public async Task<TransferTargetDecision> ResolveAsync(string rawTarget, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return TransferTargetDecision.Refuse("A transfer destination is required.");
        }

        var targetId = rawTarget.Trim();

        foreach (var targetType in _candidateTargetTypes)
        {
            var resolution = await _destinationResolver.ResolveAsync(
                new TransferRequest
                {
                    TargetType = targetType,
                    TargetId = targetId,
                    Principal = user,
                },
                user,
                cancellationToken);

            if (resolution?.Succeeded == true)
            {
                return TransferTargetDecision.Allow(resolution.ResolvedTarget);
            }
        }

        return TransferTargetDecision.Refuse(
            "The requested transfer destination is not available. Choose an approved destination, agent, or queue.");
    }
}
