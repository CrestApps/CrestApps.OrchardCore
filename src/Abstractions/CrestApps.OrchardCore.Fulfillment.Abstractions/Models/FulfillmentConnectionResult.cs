namespace CrestApps.OrchardCore.Fulfillment.Models;

/// <summary>
/// How a check of a fulfillment partner's connection went.
/// </summary>
public sealed class FulfillmentConnectionResult
{
    private FulfillmentConnectionResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>
    /// Gets a value indicating whether the partner accepted the saved credentials and the chosen account.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets what the check found, in words for the person who ran it.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates the result of a check that passed.
    /// </summary>
    /// <param name="message">What the check found.</param>
    /// <returns>The result.</returns>
    public static FulfillmentConnectionResult Success(string message)
        => new(true, message);

    /// <summary>
    /// Creates the result of a check that failed.
    /// </summary>
    /// <param name="message">Why the check failed.</param>
    /// <returns>The result.</returns>
    public static FulfillmentConnectionResult Failure(string message)
        => new(false, message);
}
