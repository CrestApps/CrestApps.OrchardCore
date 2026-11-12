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

/// <summary>
/// What became of keys offered to a secure keypad capture.
/// </summary>
internal enum SecureKeypadEntryState
{
    /// <summary>
    /// The keypad is not private on this call: the keys are the caller's turn as usual.
    /// </summary>
    NotCapturing,

    /// <summary>
    /// The keys were kept; the entry is not finished.
    /// </summary>
    Collecting,

    /// <summary>
    /// The entry is finished.
    /// </summary>
    Completed,

    /// <summary>
    /// The caller cancelled the entry.
    /// </summary>
    Cancelled,
}

/// <summary>
/// The outcome of offering keys to a secure keypad capture.
/// </summary>
/// <param name="State">What became of the keys.</param>
/// <param name="Field">What the caller was asked to type.</param>
/// <param name="Value">The finished entry, only when <see cref="SecureKeypadEntryState.Completed"/>. Never stored or logged.</param>
internal sealed record SecureKeypadEntry(SecureKeypadEntryState State, SecureCaptureField Field, string Value)
{
    /// <summary>
    /// The keys were not captured.
    /// </summary>
    public static readonly SecureKeypadEntry NotCapturing = new(SecureKeypadEntryState.NotCapturing, SecureCaptureField.Custom, null);

    /// <summary>
    /// Gets a value indicating whether the capture took the keys.
    /// </summary>
    public bool Captured => State != SecureKeypadEntryState.NotCapturing;

    /// <summary>
    /// Gets a value indicating whether the entry is over, finished or cancelled.
    /// </summary>
    public bool Ended => State is SecureKeypadEntryState.Completed or SecureKeypadEntryState.Cancelled;

    /// <inheritdoc/>
    public override string ToString()
        => $"{State} {Field}";
}
