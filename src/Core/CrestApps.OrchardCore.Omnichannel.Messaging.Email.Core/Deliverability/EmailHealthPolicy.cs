using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// The thresholds sending health is judged by, and the arithmetic of sending limits and warm-up.
/// </summary>
/// <remarks>
/// The complaint thresholds are the ones Gmail publishes for bulk senders: stay under 0.1% of mail reported as spam,
/// and never reach 0.3%. The bounce thresholds follow the common sending services, which review an account near 5%.
/// Rates are only judged once enough mail has gone out for one bad recipient not to decide it.
/// </remarks>
public static class EmailHealthPolicy
{
    /// <summary>
    /// The window sending health is measured over.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);

    /// <summary>
    /// The fewest emails in the window before the bounce rate is judged.
    /// </summary>
    public const int MinimumSentForBounceRate = 100;

    /// <summary>
    /// The fewest emails in the window before the complaint rate can pause the address.
    /// </summary>
    public const int MinimumSentForComplaintPause = 300;

    /// <summary>
    /// The hard-bounce rate that is shown as a warning.
    /// </summary>
    public const double WarningBounceRate = 0.02;

    /// <summary>
    /// The hard-bounce rate that pauses bulk sending.
    /// </summary>
    public const double PauseBounceRate = 0.05;

    /// <summary>
    /// The spam-complaint rate that is shown as a warning.
    /// </summary>
    public const double WarningComplaintRate = 0.001;

    /// <summary>
    /// The spam-complaint rate that pauses bulk sending.
    /// </summary>
    public const double PauseComplaintRate = 0.003;

    /// <summary>
    /// The soft bounces to one recipient, within <see cref="SoftBounceWindow"/>, after which it is suppressed.
    /// </summary>
    public const int SoftBouncesBeforeSuppression = 3;

    /// <summary>
    /// The window soft bounces to one recipient are counted over.
    /// </summary>
    public static readonly TimeSpan SoftBounceWindow = TimeSpan.FromDays(14);

    /// <summary>
    /// The deferrals from one receiving domain, within <see cref="DomainDeferralWindow"/>, after which bulk mail to
    /// that domain waits.
    /// </summary>
    public const int DomainDeferralsBeforeBackoff = 3;

    /// <summary>
    /// The window deferrals from one receiving domain are counted over.
    /// </summary>
    public static readonly TimeSpan DomainDeferralWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long bulk mail to a receiving domain waits once it has deferred too much.
    /// </summary>
    public static readonly TimeSpan DomainBackoff = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The emails the address may send in 24 hours at <paramref name="utcNow"/>, warm-up included.
    /// </summary>
    /// <param name="limits">The address's limits.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns>The limit, or zero for none.</returns>
    public static int GetDailyLimit(EmailSendingLimits limits, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var configured = limits.MaxPerDay > 0 ? limits.MaxPerDay : 0;

        if (!limits.WarmUp)
        {
            return configured;
        }

        var start = limits.WarmUpStartedUtc ?? utcNow;
        var day = Math.Max(0, (int)Math.Floor((utcNow - start).TotalDays));
        var first = Math.Max(1, limits.WarmUpFirstDayLimit);

        // Doubling past a month is past any limit an address would have; stopping there keeps the shift in range.
        var warmUpLimit = day >= 30 ? long.MaxValue : first * (1L << day);

        return configured == 0
            ? (int)Math.Min(warmUpLimit, int.MaxValue)
            : (int)Math.Min(warmUpLimit, configured);
    }

    /// <summary>
    /// Gets whether the address is still warming up at <paramref name="utcNow"/>: its daily limit is below the one set.
    /// </summary>
    /// <param name="limits">The address's limits.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns><see langword="true"/> while warming up.</returns>
    public static bool IsWarmingUp(EmailSendingLimits limits, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (!limits.WarmUp)
        {
            return false;
        }

        var daily = GetDailyLimit(limits, utcNow);

        return limits.MaxPerDay > 0 ? daily < limits.MaxPerDay : daily < int.MaxValue;
    }

    /// <summary>
    /// The spacing between two turns of held-back bulk mail: the address's binding pace.
    /// </summary>
    /// <param name="limits">The address's limits.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns>The spacing.</returns>
    public static TimeSpan GetTurnInterval(EmailSendingLimits limits, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var seconds = Math.Max(0, limits.MinimumSecondsBetweenSends);

        if (limits.MaxPerHour > 0)
        {
            seconds = Math.Max(seconds, (int)Math.Ceiling(3600d / limits.MaxPerHour));
        }

        // While warming up, a day's small allowance is spread over twelve hours rather than sent in one burst.
        if (IsWarmingUp(limits, utcNow))
        {
            seconds = Math.Max(seconds, (int)Math.Ceiling(43200d / Math.Max(1, GetDailyLimit(limits, utcNow))));
        }

        return TimeSpan.FromSeconds(Math.Max(1, seconds));
    }

    /// <summary>
    /// Judges sending health from the window's counts.
    /// </summary>
    /// <param name="sent">The emails sent.</param>
    /// <param name="hardBounces">The hard bounces.</param>
    /// <param name="complaints">The spam complaints.</param>
    /// <returns>The level.</returns>
    public static EmailHealthLevel Judge(int sent, int hardBounces, int complaints)
    {
        if (ShouldPause(sent, hardBounces, complaints))
        {
            return EmailHealthLevel.Poor;
        }

        var bounceRate = Rate(hardBounces, sent);
        var complaintRate = Rate(complaints, sent);

        return (sent >= MinimumSentForBounceRate && (bounceRate >= WarningBounceRate || complaintRate >= WarningComplaintRate))
            ? EmailHealthLevel.Warning
            : EmailHealthLevel.Healthy;
    }

    /// <summary>
    /// Gets whether the window's counts should pause bulk sending.
    /// </summary>
    /// <param name="sent">The emails sent.</param>
    /// <param name="hardBounces">The hard bounces.</param>
    /// <param name="complaints">The spam complaints.</param>
    /// <returns><see langword="true"/> when bulk sending should pause.</returns>
    public static bool ShouldPause(int sent, int hardBounces, int complaints)
        => (sent >= MinimumSentForBounceRate && Rate(hardBounces, sent) >= PauseBounceRate) ||
            (sent >= MinimumSentForComplaintPause && Rate(complaints, sent) >= PauseComplaintRate);

    /// <summary>
    /// A rate, zero when nothing was sent.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="sent">The emails sent.</param>
    /// <returns>The rate, from zero to one.</returns>
    public static double Rate(int count, int sent)
        => sent <= 0 ? 0 : (double)count / sent;
}

/// <summary>
/// How healthy an address's sending is.
/// </summary>
public enum EmailHealthLevel
{
    /// <summary>
    /// Bounces and complaints are low.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Bounces or complaints are high enough to act on before providers do.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Bounces or complaints are at the level that gets senders blocked.
    /// </summary>
    Poor = 2,
}
