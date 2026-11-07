namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// How fast an email address may send bulk mail: broadcasts, campaign opening emails and their follow-ups. Replies in a
/// conversation the customer started are counted against these limits but never held back by them.
/// </summary>
/// <remarks>
/// The defaults sit inside what the common mailbox hosts allow one mailbox (Microsoft 365 takes about 30 messages a
/// minute and 10,000 recipients a day; Google Workspace about 2,000 a day). An address on a dedicated sending service
/// can raise them, gradually, once it has a sending history. Zero means no limit.
/// </remarks>
public sealed class EmailSendingLimits
{
    /// <summary>
    /// Gets or sets the most emails the address sends in any hour. Zero means no limit.
    /// </summary>
    public int MaxPerHour { get; set; } = 200;

    /// <summary>
    /// Gets or sets the most emails the address sends in any 24 hours. Zero means no limit.
    /// </summary>
    public int MaxPerDay { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the most bulk emails the address sends to one receiving domain (gmail.com, outlook.com) in any
    /// hour, so one large mailbox provider never sees a burst. Zero means no limit.
    /// </summary>
    public int MaxPerHourPerDomain { get; set; }

    /// <summary>
    /// Gets or sets the shortest time, in seconds, between two bulk emails from the address. Zero means none.
    /// </summary>
    public int MinimumSecondsBetweenSends { get; set; } = 2;

    /// <summary>
    /// Gets or sets a value indicating whether the address is warming up: its daily limit starts at
    /// <see cref="WarmUpFirstDayLimit"/> and doubles every day until it reaches <see cref="MaxPerDay"/>.
    /// </summary>
    public bool WarmUp { get; set; }

    /// <summary>
    /// Gets or sets when the warm-up started. Set when warm-up is turned on.
    /// </summary>
    public DateTime? WarmUpStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets how many emails the address may send on the first day of its warm-up.
    /// </summary>
    public int WarmUpFirstDayLimit { get; set; } = 50;

    /// <summary>
    /// Gets or sets a value indicating whether bulk sending is paused, until someone resumes it, when the address's
    /// bounce or spam-complaint rate passes the level that gets senders blocked.
    /// </summary>
    public bool PauseOnPoorHealth { get; set; } = true;
}
