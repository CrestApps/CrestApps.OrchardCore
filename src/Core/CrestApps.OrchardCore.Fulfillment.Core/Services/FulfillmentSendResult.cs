using CrestApps.OrchardCore.Fulfillment.Core.Models;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// How sending one fulfillment request ended for this caller.
/// </summary>
/// <param name="Request">The request as stored, or as it was loaded when another worker took it.</param>
/// <param name="TakenByAnotherWorker">Whether another worker was sending the request, so this caller left it alone.</param>
public sealed record FulfillmentSendResult(FulfillmentRequest Request, bool TakenByAnotherWorker = false);
