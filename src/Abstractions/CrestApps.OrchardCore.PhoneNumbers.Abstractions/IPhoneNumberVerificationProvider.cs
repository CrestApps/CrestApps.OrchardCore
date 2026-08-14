namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Represents a single phone number verification provider implementation.
/// Implementations are registered against a unique provider key and are
/// resolved by the verification manager; they never select themselves.
/// </summary>
public interface IPhoneNumberVerificationProvider
{
    /// <summary>
    /// Verifies the given phone number against the external provider and maps the
    /// provider response into the common <see cref="PhoneNumberVerificationResult"/> model.
    /// </summary>
    /// <param name="phoneNumber">The phone number to verify, preferably in E.164 format.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A provider-agnostic verification result.</returns>
    Task<PhoneNumberVerificationResult> VerifyAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);
}
