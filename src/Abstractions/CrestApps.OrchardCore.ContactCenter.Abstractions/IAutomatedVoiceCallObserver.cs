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
/// <para>
/// A moment can be reported more than once, so an observer keeps the first report of each and ignores the rest.
/// Providers redeliver the events that report them, and a conversation handed to a person is reported ended when
/// the assistant's part of it ended and again when the caller finally hangs up on the agent. The first report
/// carries the time the moment happened.
/// </para>
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
