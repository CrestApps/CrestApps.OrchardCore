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
    /// How long a capture waits for the caller before it times out.
    /// </summary>
    public static readonly TimeSpan Expiry = TimeSpan.FromMinutes(3);

    /// <summary>
    /// How long a node keeps its own record of a timed-out entry in the cache, counted from the last key the caller typed.
    /// The keypad stays private after that too, from the window on the activity, until the caller presses the star key,
    /// the AI asks for a new entry or the call ends.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

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
