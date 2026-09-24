using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Told what happens on an automated voice agent's call: that it was answered, who answered it, and how the
/// conversation ended.
/// </summary>
/// <remarks>
/// The automated voice module runs on tenants with no Contact Center, so it reports these moments through this
/// abstraction rather than writing any Contact Center record itself; the Contact Center implements it to put them
/// in its audit log. Every registered observer is told, and none of them can affect the call: an observer that
/// fails is logged and the conversation carries on.
/// </remarks>
public interface IAutomatedVoiceCallObserver
{
    /// <summary>
    /// Records one moment of an automated call.
    /// </summary>
    /// <param name="observation">What happened.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ObserveAsync(AutomatedVoiceCallObservation observation, CancellationToken cancellationToken = default);
}
