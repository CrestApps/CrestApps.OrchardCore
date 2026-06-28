namespace CrestApps.OrchardCore.PhoneNumbers.Core.Services;

/// <summary>
/// Handles events raised during the phone number verification lifecycle.
/// </summary>
public interface IPhoneNumberVerificationHandler
{
    /// <summary>
    /// Invoked immediately before a phone number is verified against a provider.
    /// </summary>
    /// <param name="context">The verification context.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task VerifyingAsync(
        PhoneNumberVerificationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked immediately after a phone number is verified and a result is produced.
    /// </summary>
    /// <param name="context">The verification context, including the populated result.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task VerifiedAsync(
        PhoneNumberVerificationContext context,
        CancellationToken cancellationToken = default);
}
