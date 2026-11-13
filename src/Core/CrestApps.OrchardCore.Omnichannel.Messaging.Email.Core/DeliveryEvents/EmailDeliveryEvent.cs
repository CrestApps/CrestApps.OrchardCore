using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;

/// <summary>
/// Something a provider reported about an email after it left: a bounce, a spam complaint, a block, a deferral.
/// </summary>
public sealed class EmailDeliveryEvent
{
    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public EmailDeliveryEventKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the recipient it happened to.
    /// </summary>
    public string Recipient { get; set; }

    /// <summary>
    /// Gets or sets the email's Message-ID or the provider's identifier for it, when the provider gave one.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the event, so a redelivered event is acted on once.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the status code the receiving server gave, such as <c>5.1.1</c>.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets what the receiving server or provider said.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets when it happened.
    /// </summary>
    public DateTime? OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the provider that reported it.
    /// </summary>
    public string Provider { get; set; }
}

/// <summary>
/// Reads one provider's delivery events webhook.
/// </summary>
public interface IEmailDeliveryEventParser
{
    /// <summary>
    /// Gets the provider name, which is the last segment of the webhook's route.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Reads the events from a webhook call.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="settings">The site's inbound email settings (signing keys, allowed topics).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The events, or why the call was refused.</returns>
    Task<EmailDeliveryEventParseResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a delivery events webhook call carried.
/// </summary>
public sealed class EmailDeliveryEventParseResult
{
    /// <summary>
    /// Gets the events.
    /// </summary>
    public IList<EmailDeliveryEvent> Events { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the call failed its signature or source check.
    /// </summary>
    public bool IsUnauthorized { get; init; }

    /// <summary>
    /// Gets a value indicating whether the call could not be read.
    /// </summary>
    public bool IsInvalid { get; init; }

    /// <summary>
    /// Gets why the call was refused, or what was done with a call that carried no events.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// The call carried these events.
    /// </summary>
    /// <param name="events">The events.</param>
    /// <returns>The result.</returns>
    public static EmailDeliveryEventParseResult Of(IList<EmailDeliveryEvent> events) => new() { Events = events ?? [] };

    /// <summary>
    /// The call was handled and carried no events (a subscription confirmation, an event kind the webhook ignores).
    /// </summary>
    /// <param name="reason">What was done.</param>
    /// <returns>The result.</returns>
    public static EmailDeliveryEventParseResult Handled(string reason) => new() { Reason = reason };

    /// <summary>
    /// The call failed its signature or source check.
    /// </summary>
    /// <param name="reason">Why.</param>
    /// <returns>The result.</returns>
    public static EmailDeliveryEventParseResult Unauthorized(string reason) => new() { IsUnauthorized = true, Reason = reason };

    /// <summary>
    /// The call could not be read.
    /// </summary>
    /// <param name="reason">Why.</param>
    /// <returns>The result.</returns>
    public static EmailDeliveryEventParseResult Invalid(string reason) => new() { IsInvalid = true, Reason = reason };
}
