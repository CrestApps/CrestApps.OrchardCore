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
    /// Gets a value indicating whether the provider may have executed the operation but its outcome could not be observed.
    /// </summary>
    public bool OutcomeUnknown { get; init; }

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

    /// <summary>
    /// Creates a result for an operation whose provider outcome could not be determined.
    /// </summary>
    /// <param name="error">The error message describing why the outcome is unknown.</param>
    /// <returns>An indeterminate <see cref="TelephonyResult"/>.</returns>
    public static TelephonyResult Unknown(string error)
    {
        return new TelephonyResult
        {
            Succeeded = false,
            OutcomeUnknown = true,
            Error = error,
        };
    }
}
