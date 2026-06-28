namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Delays consecutive phone number verification provider requests.
/// </summary>
internal interface IPhoneNumberVerificationRequestDelayer
{
    /// <summary>
    /// Delays execution before the next provider request.
    /// </summary>
    /// <param name="delayMilliseconds">The delay duration, in milliseconds.</param>
    /// <param name="cancellationToken">A token used to cancel the delay.</param>
    /// <returns>A task that completes when the delay finishes.</returns>
    Task DelayAsync(int delayMilliseconds, CancellationToken cancellationToken = default);
}
