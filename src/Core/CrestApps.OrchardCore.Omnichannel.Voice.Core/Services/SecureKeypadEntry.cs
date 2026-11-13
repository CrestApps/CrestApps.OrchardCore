using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The outcome of offering keys to a secure keypad capture.
/// </summary>
/// <param name="State">What became of the keys.</param>
/// <param name="Field">What the caller was asked to type.</param>
/// <param name="Value">The finished entry, only when <see cref="SecureKeypadEntryState.Completed"/>. Never stored or logged.</param>
public sealed record SecureKeypadEntry(SecureKeypadEntryState State, SecureCaptureField Field, string Value)
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
