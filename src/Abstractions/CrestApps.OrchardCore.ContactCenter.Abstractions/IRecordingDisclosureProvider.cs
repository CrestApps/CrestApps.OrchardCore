using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Supplies the notice a caller is given that the call is recorded, such as "This call may be recorded for quality
/// and training purposes."
/// </summary>
/// <remarks>
/// Registered only while call recording is enabled, so a consumer that finds no provider has nothing to disclose.
/// The text is given word for word: a legal notice that is paraphrased is not the notice the tenant approved.
/// </remarks>
public interface IRecordingDisclosureProvider
{
    /// <summary>
    /// Gets the disclosure to give on a call of the specified type.
    /// </summary>
    /// <param name="callType">Who gives the disclosure on the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The disclosure text, or <see langword="null"/> when none is given on this type of call.</returns>
    Task<string> GetDisclosureAsync(RecordingDisclosureCallType callType, CancellationToken cancellationToken = default);
}
