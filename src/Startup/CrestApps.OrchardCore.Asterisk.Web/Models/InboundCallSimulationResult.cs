namespace CrestApps.OrchardCore.Asterisk.Web.Models;

/// <summary>
/// Describes the outcome of one simulated inbound voice event.
/// </summary>
public sealed class InboundCallSimulationResult
{
    /// <summary>
    /// Gets or sets the provider call identifier sent to Orchard.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the simulator originated a matching loopback channel in Asterisk.
    /// </summary>
    public bool AsteriskOriginated { get; set; }

    /// <summary>
    /// Gets or sets the Asterisk channel identifier, when the simulator created one.
    /// </summary>
    public string AsteriskChannelId { get; set; }

    /// <summary>
    /// Gets or sets the generated caller number.
    /// </summary>
    public string CallerNumber { get; set; }

    /// <summary>
    /// Gets or sets the generated caller display name.
    /// </summary>
    public string CallerName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the ingress request succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code returned by Orchard.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets how long the request took, in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call was offered to an agent.
    /// </summary>
    public bool? Routed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call is waiting in a Contact Center queue.
    /// </summary>
    public bool? Queued { get; set; }

    /// <summary>
    /// Gets or sets the interaction identifier created for the simulated call.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the activity identifier created for the simulated call.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the queue identifier selected by routing.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the routed agent user identifier, when one was reserved.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the routing reason returned by Orchard.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the raw response body returned by Orchard.
    /// </summary>
    public string RawResponse { get; set; }
}
