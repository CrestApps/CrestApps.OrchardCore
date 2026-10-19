using System.Text.Json.Serialization;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// A buyer's request to send back items of an order, for their money back or for something else, and what the store
/// did with it.
/// </summary>
public sealed class OrderReturn : CatalogItem
{
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number the buyer and the store know the return by, such as <c>R-261019-K3QZ</c>.
    /// </summary>
    public string Number { get; set; }

    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    public string UserId { get; set; }

    public string CustomerName { get; set; }

    public string CustomerEmail { get; set; }

    public string Currency { get; set; }

    public List<OrderReturnLine> Lines { get; set; } = [];

    public OrderReturnResolution Resolution { get; set; }

    /// <summary>
    /// Gets or sets what the buyer would like instead, for an exchange.
    /// </summary>
    public string ExchangeFor { get; set; }

    /// <summary>
    /// Gets or sets what the buyer wrote.
    /// </summary>
    public string Comment { get; set; }

    public OrderReturnStatus Status { get; set; }

    /// <summary>
    /// Gets or sets what the store told the buyer when it approved or declined the return.
    /// </summary>
    public string StoreMessage { get; set; }

    /// <summary>
    /// Gets or sets the money given back for the return.
    /// </summary>
    public decimal RefundedAmount { get; set; }

    /// <summary>
    /// Gets or sets the location returned units went back into stock at, or <see langword="null"/>.
    /// </summary>
    public string RestockLocationId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public DateTime? ApprovedUtc { get; set; }

    public DateTime? ReceivedUtc { get; set; }

    public DateTime? ClosedUtc { get; set; }

    [JsonIgnore]
    public int Quantity
        => Lines.Sum(line => line.Quantity);

    /// <summary>
    /// Gets whether the return still counts against what can be returned: anything but declined or withdrawn.
    /// </summary>
    [JsonIgnore]
    public bool IsActive
        => Status is not (OrderReturnStatus.Rejected or OrderReturnStatus.Canceled);

    [JsonIgnore]
    public bool IsOpen
        => Status is OrderReturnStatus.Requested or OrderReturnStatus.Approved or OrderReturnStatus.Received;
}

/// <summary>
/// Units of one line of the order being sent back, and why.
/// </summary>
public sealed class OrderReturnLine
{
    public string LineId { get; set; }

    public string Title { get; set; }

    public string Sku { get; set; }

    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets what one unit was paid, from the order.
    /// </summary>
    public decimal UnitPrice { get; set; }

    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets whether the units went back into stock when they arrived.
    /// </summary>
    public bool Restocked { get; set; }
}

public enum OrderReturnResolution
{
    Refund = 0,

    Exchange = 1,
}

public enum OrderReturnStatus
{
    /// <summary>
    /// The buyer asked; the store hasn't answered.
    /// </summary>
    Requested = 0,

    /// <summary>
    /// The store agreed, and the buyer was told how to send the items back.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// The items arrived back.
    /// </summary>
    Received = 2,

    /// <summary>
    /// Done: refunded, or exchanged.
    /// </summary>
    Completed = 3,

    Rejected = 4,

    /// <summary>
    /// The buyer withdrew it before the store answered.
    /// </summary>
    Canceled = 5,
}
