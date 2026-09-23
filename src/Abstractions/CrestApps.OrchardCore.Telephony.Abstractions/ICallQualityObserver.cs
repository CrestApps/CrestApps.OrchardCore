using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Receives the quality of each call leg once the leg is over.
/// </summary>
/// <remarks>
/// Implemented by whatever keeps a record of call quality, such as the contact center, which can tie a leg to the
/// interaction and agent it belongs to. Every registered observer is told; one that fails does not stop the others,
/// and none of them can affect the call, which is already over.
/// </remarks>
public interface ICallQualityObserver
{
    /// <summary>
    /// Records one leg's quality.
    /// </summary>
    /// <param name="observation">The measurement.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ObserveAsync(CallQualityObservation observation, CancellationToken cancellationToken = default);
}
