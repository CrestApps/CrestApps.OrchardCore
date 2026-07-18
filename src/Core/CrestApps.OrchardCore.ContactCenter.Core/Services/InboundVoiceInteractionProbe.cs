using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Determines whether a durable, still-active Contact Center interaction exists for a provider call.
/// </summary>
public sealed class InboundVoiceInteractionProbe : IInboundVoiceInteractionProbe
{
    private readonly IInteractionManager _interactionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundVoiceInteractionProbe"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to resolve provider calls.</param>
    public InboundVoiceInteractionProbe(IInteractionManager interactionManager)
    {
        _interactionManager = interactionManager;
    }

    /// <inheritdoc/>
    public async Task<bool> HasActiveInteractionAsync(
        string providerName,
        string providerCallId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return false;
        }

        var interaction = string.IsNullOrWhiteSpace(providerName)
            ? await _interactionManager.FindByProviderInteractionIdAsync(providerCallId, cancellationToken)
            : await _interactionManager.FindByProviderInteractionIdAsync(providerName, providerCallId, cancellationToken);

        return interaction is not null &&
            interaction.Status is not (InteractionStatus.Ended or InteractionStatus.Failed);
    }
}
