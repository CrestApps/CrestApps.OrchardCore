using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The rules of a secure keypad entry: a card, account or identity number a caller types on an AI call instead of
/// saying it. The entry ends with <c>#</c> or when the field is full, and <c>*</c> cancels it. The entry itself is kept
/// by <see cref="ISecureKeypadCaptureStore"/>, where every node finds it, and never in the transcript, the activity or
/// a log.
/// </summary>
internal static class SecureKeypadCaptures
{
    /// <summary>
    /// How long a capture waits for the caller before it is forgotten.
    /// </summary>
    public static readonly TimeSpan Expiry = TimeSpan.FromMinutes(3);

    /// <summary>
    /// The most keys an entry of a kind takes, after which it ends without <c>#</c>.
    /// </summary>
    /// <param name="field">The kind of entry.</param>
    /// <returns>The length.</returns>
    public static int MaxLength(SecureCaptureField field)
        => field switch
        {
            SecureCaptureField.CreditCardNumber => 19,
            SecureCaptureField.CardExpiry => 4,
            SecureCaptureField.CardSecurityCode => 4,
            SecureCaptureField.BankAccountNumber => 17,
            SecureCaptureField.NationalId => 12,
            _ => 32,
        };
}
