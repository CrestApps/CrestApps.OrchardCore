namespace CrestApps.OrchardCore.Outreach.Models;

/// <summary>
/// The outcome of sending one message. A sender throws instead when the failure is transient, so the batch is retried.
/// </summary>
public sealed class OutreachSendResult
{
    private static readonly OutreachSendResult _success = new() { Succeeded = true };

    /// <summary>
    /// Gets whether the message was accepted for delivery.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets why the message was not sent.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Returns a successful result.
    /// </summary>
    /// <returns>The result.</returns>
    public static OutreachSendResult Success()
        => _success;

    /// <summary>
    /// Returns a failed result.
    /// </summary>
    /// <param name="error">Why the message was not sent.</param>
    /// <returns>The result.</returns>
    public static OutreachSendResult Failure(string error)
        => new() { Error = error };
}
