using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Acts on a waiting caller's answer to the queue's callback offer ("press 1 and we will call you back").
/// </summary>
public interface IQueueCallbackOfferResponder
{
    /// <summary>
    /// Applies the caller's key press to the callback offer they were just played. Accepting schedules the callback,
    /// keeping their place in line, confirms it to them and ends the call; anything else puts them back to their
    /// music and they keep waiting.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="digitsEvent">What the provider reported the caller pressed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the key press answered an outstanding callback offer.</returns>
    Task<bool> HandleAsync(Interaction interaction, InboundVoiceDigitsEvent digitsEvent, CancellationToken cancellationToken = default);
}
