namespace CrestApps.OrchardCore.Fulfillment.Models;

/// <summary>
/// The result of sending an order to a fulfillment partner.
/// </summary>
public sealed class FulfillmentSubmitResult
{
    private FulfillmentSubmitResult(FulfillmentSubmitStatus status, string externalOrderId, string message)
    {
        Status = status;
        ExternalOrderId = externalOrderId;
        Message = message;
    }

    /// <summary>
    /// Gets how sending ended.
    /// </summary>
    public FulfillmentSubmitStatus Status { get; }

    /// <summary>
    /// Gets the partner's identifier of the order it created, when it created one.
    /// </summary>
    public string ExternalOrderId { get; }

    /// <summary>
    /// Gets what happened, in words for the store owner.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates the result of an order the partner accepted.
    /// </summary>
    /// <param name="externalOrderId">The partner's identifier of the order.</param>
    /// <param name="message">What was sent.</param>
    /// <returns>The result.</returns>
    public static FulfillmentSubmitResult Sent(string externalOrderId, string message)
        => new(FulfillmentSubmitStatus.Sent, externalOrderId, message);

    /// <summary>
    /// Creates the result of an order with nothing for this partner.
    /// </summary>
    /// <param name="message">Why nothing was sent.</param>
    /// <returns>The result.</returns>
    public static FulfillmentSubmitResult NothingToSend(string message)
        => new(FulfillmentSubmitStatus.NothingToSend, null, message);

    /// <summary>
    /// Creates the result of an order that may be sent again later.
    /// </summary>
    /// <param name="message">Why it was not sent now.</param>
    /// <returns>The result.</returns>
    public static FulfillmentSubmitResult RetryLater(string message)
        => new(FulfillmentSubmitStatus.RetryLater, null, message);

    /// <summary>
    /// Creates the result of an order a person must look at.
    /// </summary>
    /// <param name="message">What the person must check or fix.</param>
    /// <returns>The result.</returns>
    public static FulfillmentSubmitResult NeedsAttention(string message)
        => new(FulfillmentSubmitStatus.NeedsAttention, null, message);
}
