using System.Collections.Concurrent;
using System.Text;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The keys a caller types on an AI call while the keypad is private: a card, account or identity number collected
/// for secure data capture instead of being heard by the AI.
/// </summary>
/// <remarks>
/// Kept in memory only, per activity, on the node holding the call, and never written to the transcript, the
/// activity or a log. The entry ends with <c>#</c> or when the field is full, and <c>*</c> cancels it. An entry nobody
/// finishes is forgotten after <see cref="Expiry"/>.
/// </remarks>
internal static class SecureKeypadCaptures
{
    /// <summary>
    /// How long a capture waits for the caller before it is forgotten.
    /// </summary>
    public static readonly TimeSpan Expiry = TimeSpan.FromMinutes(3);

    private static readonly ConcurrentDictionary<string, Capture> _captures = new(StringComparer.Ordinal);

    /// <summary>
    /// Makes the caller's keypad private on a call until one entry is finished.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="field">What the caller is asked to type.</param>
    /// <param name="nowUtc">The current time.</param>
    public static void Start(string activityId, SecureCaptureField field, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityId);

        _captures[activityId] = new Capture(field, nowUtc);
    }

    /// <summary>
    /// Whether the caller's keypad is private on a call.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> while an entry is being collected.</returns>
    public static bool IsCapturing(string activityId, DateTime nowUtc)
        => !string.IsNullOrEmpty(activityId) &&
            _captures.TryGetValue(activityId, out var capture) &&
            nowUtc - capture.StartedUtc <= Expiry;

    /// <summary>
    /// Offers keys the caller pressed to the capture on their call.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="digits">The keys.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>What became of the keys.</returns>
    public static SecureKeypadEntry Offer(string activityId, string digits, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(activityId) || !_captures.TryGetValue(activityId, out var capture))
        {
            return SecureKeypadEntry.NotCapturing;
        }

        if (nowUtc - capture.StartedUtc > Expiry)
        {
            _captures.TryRemove(activityId, out _);

            return SecureKeypadEntry.NotCapturing;
        }

        lock (capture)
        {
            foreach (var key in digits ?? string.Empty)
            {
                if (key == '*')
                {
                    _captures.TryRemove(activityId, out _);

                    return new SecureKeypadEntry(SecureKeypadEntryState.Cancelled, capture.Field, null);
                }

                if (key == '#' || capture.Value.Length >= MaxLength(capture.Field))
                {
                    return Complete(activityId, capture);
                }

                if (char.IsAsciiDigit(key))
                {
                    capture.Value.Append(key);
                }
            }

            return capture.Value.Length >= MaxLength(capture.Field)
                ? Complete(activityId, capture)
                : new SecureKeypadEntry(SecureKeypadEntryState.Collecting, capture.Field, null);
        }
    }

    /// <summary>
    /// Stops collecting on a call, such as when it ends.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    public static void Forget(string activityId)
    {
        if (!string.IsNullOrEmpty(activityId))
        {
            _captures.TryRemove(activityId, out _);
        }
    }

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

    private static SecureKeypadEntry Complete(string activityId, Capture capture)
    {
        _captures.TryRemove(activityId, out _);

        var value = capture.Value.ToString();
        capture.Value.Clear();

        return new SecureKeypadEntry(SecureKeypadEntryState.Completed, capture.Field, value);
    }

    private sealed class Capture
    {
        public Capture(SecureCaptureField field, DateTime startedUtc)
        {
            Field = field;
            StartedUtc = startedUtc;
        }

        public SecureCaptureField Field { get; }

        public DateTime StartedUtc { get; }

        public StringBuilder Value { get; } = new();
    }
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
