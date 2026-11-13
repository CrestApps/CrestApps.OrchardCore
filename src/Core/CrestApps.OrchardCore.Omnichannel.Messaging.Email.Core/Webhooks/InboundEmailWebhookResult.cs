using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// What a webhook parser read from one call.
/// </summary>
public sealed class InboundEmailWebhookResult
{
    /// <summary>
    /// Gets the emails the call carried. Most providers send one per call.
    /// </summary>
    public IList<InboundEmail> Emails { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the call failed the provider's own signature check.
    /// </summary>
    public bool IsUnauthorized { get; init; }

    /// <summary>
    /// Gets a value indicating whether the call could not be read at all.
    /// </summary>
    public bool IsInvalid { get; init; }

    /// <summary>
    /// Gets why the call was refused or carried nothing, for the log.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Creates a result carrying one email.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <returns>The result.</returns>
    public static InboundEmailWebhookResult Of(InboundEmail email)
        => new() { Emails = [email] };

    /// <summary>
    /// Creates a result for a call that carried no email but was handled, such as a subscription confirmation.
    /// </summary>
    /// <param name="reason">What the call was.</param>
    /// <returns>The result.</returns>
    public static InboundEmailWebhookResult Handled(string reason)
        => new() { Reason = reason };

    /// <summary>
    /// Creates a result for a call that failed the provider's signature check.
    /// </summary>
    /// <param name="reason">Why.</param>
    /// <returns>The result.</returns>
    public static InboundEmailWebhookResult Unauthorized(string reason)
        => new() { IsUnauthorized = true, Reason = reason };

    /// <summary>
    /// Creates a result for a call that could not be read.
    /// </summary>
    /// <param name="reason">Why.</param>
    /// <returns>The result.</returns>
    public static InboundEmailWebhookResult Invalid(string reason)
        => new() { IsInvalid = true, Reason = reason };
}
