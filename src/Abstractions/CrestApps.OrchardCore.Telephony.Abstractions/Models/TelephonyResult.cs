namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the outcome of a telephony operation.
/// </summary>
public sealed class TelephonyResult
{
    /// <summary>
    /// Gets a value indicating whether the operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the error message describing why the operation failed, when <see cref="Succeeded"/> is
    /// <see langword="false"/>.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Gets the call affected by the operation, when applicable.
    /// </summary>
    public TelephonyCall Call { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="call">The call affected by the operation, when applicable.</param>
    /// <returns>A successful <see cref="TelephonyResult"/>.</returns>
    public static TelephonyResult Success(TelephonyCall call = null)
        => new() { Succeeded = true, Call = call };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A failed <see cref="TelephonyResult"/>.</returns>
    public static TelephonyResult Failed(string error)
        => new() { Succeeded = false, Error = error };
}
