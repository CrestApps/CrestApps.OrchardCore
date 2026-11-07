using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// Whether one email may leave an address now, later, or not at all.
/// </summary>
public sealed class EmailSendDecision
{
    private static readonly EmailSendDecision _allowed = new() { IsAllowed = true };

    /// <summary>
    /// Gets a value indicating whether the email may leave now.
    /// </summary>
    public bool IsAllowed { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the email may never leave (the recipient is suppressed).
    /// </summary>
    public bool IsRefused { get; private init; }

    /// <summary>
    /// Gets when the email may leave, for one held back.
    /// </summary>
    public DateTime? RetryAfterUtc { get; private init; }

    /// <summary>
    /// Gets why the email was held back or refused.
    /// </summary>
    public LocalizedString Reason { get; private init; }

    /// <summary>
    /// The email may leave now.
    /// </summary>
    /// <returns>The decision.</returns>
    public static EmailSendDecision Allow() => _allowed;

    /// <summary>
    /// The email may never leave.
    /// </summary>
    /// <param name="reason">Why.</param>
    /// <returns>The decision.</returns>
    public static EmailSendDecision Refuse(LocalizedString reason) => new() { IsRefused = true, Reason = reason };

    /// <summary>
    /// The email may leave at <paramref name="retryAfterUtc"/>.
    /// </summary>
    /// <param name="retryAfterUtc">When.</param>
    /// <param name="reason">Why it waits.</param>
    /// <returns>The decision.</returns>
    public static EmailSendDecision Defer(DateTime retryAfterUtc, LocalizedString reason) => new() { RetryAfterUtc = retryAfterUtc, Reason = reason };
}

/// <summary>
/// An address's sending health and where it stands against its limits, for its editor.
/// </summary>
public sealed class EmailAddressHealth
{
    /// <summary>
    /// Gets or sets the emails sent in the health window.
    /// </summary>
    public int Sent { get; set; }

    /// <summary>
    /// Gets or sets the hard bounces in the window.
    /// </summary>
    public int HardBounces { get; set; }

    /// <summary>
    /// Gets or sets the soft bounces in the window.
    /// </summary>
    public int SoftBounces { get; set; }

    /// <summary>
    /// Gets or sets the spam complaints in the window.
    /// </summary>
    public int Complaints { get; set; }

    /// <summary>
    /// Gets or sets the blocks in the window.
    /// </summary>
    public int Blocks { get; set; }

    /// <summary>
    /// Gets the hard-bounce rate.
    /// </summary>
    public double BounceRate => EmailHealthPolicy.Rate(HardBounces, Sent);

    /// <summary>
    /// Gets the spam-complaint rate.
    /// </summary>
    public double ComplaintRate => EmailHealthPolicy.Rate(Complaints, Sent);

    /// <summary>
    /// Gets or sets how healthy the sending is.
    /// </summary>
    public EmailHealthLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the emails sent in the last hour.
    /// </summary>
    public int SentLastHour { get; set; }

    /// <summary>
    /// Gets or sets the emails sent in the last 24 hours.
    /// </summary>
    public int SentLastDay { get; set; }

    /// <summary>
    /// Gets or sets today's daily limit, warm-up included; zero for none.
    /// </summary>
    public int DailyLimit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the address is warming up.
    /// </summary>
    public bool IsWarmingUp { get; set; }

    /// <summary>
    /// Gets or sets the address's sending state, or <see langword="null"/> when nothing has happened to it yet.
    /// </summary>
    public EmailSendingState State { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether bulk sending is paused now.
    /// </summary>
    public bool IsPaused { get; set; }
}
