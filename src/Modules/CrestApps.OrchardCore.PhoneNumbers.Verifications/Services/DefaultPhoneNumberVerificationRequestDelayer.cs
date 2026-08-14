namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Default delay implementation for phone number verification provider requests.
/// </summary>
internal sealed class DefaultPhoneNumberVerificationRequestDelayer : IPhoneNumberVerificationRequestDelayer
{
    /// <inheritdoc/>
    public Task DelayAsync(int delayMilliseconds, CancellationToken cancellationToken = default)
    {
        return Task.Delay(delayMilliseconds, cancellationToken);
    }
}
