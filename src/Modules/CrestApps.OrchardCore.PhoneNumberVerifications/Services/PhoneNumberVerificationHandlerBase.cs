namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Provides a no-op base implementation of <see cref="IPhoneNumberVerificationHandler"/>
/// so handlers only need to override the events they care about.
/// </summary>
public abstract class PhoneNumberVerificationHandlerBase : IPhoneNumberVerificationHandler
{
    /// <inheritdoc/>
    public virtual Task VerifyingAsync(
        PhoneNumberVerificationContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task VerifiedAsync(
        PhoneNumberVerificationContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
