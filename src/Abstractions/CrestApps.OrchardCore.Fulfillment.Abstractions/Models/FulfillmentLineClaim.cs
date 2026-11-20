namespace CrestApps.OrchardCore.Fulfillment.Models;

/// <summary>
/// A partner's claim on an order line: how many units of it the partner makes.
/// </summary>
/// <param name="LineId">The store's identifier of the order line.</param>
/// <param name="Quantity">How many units the partner makes; the engine caps it to what is left to ship.</param>
public sealed record FulfillmentLineClaim(string LineId, int Quantity);
