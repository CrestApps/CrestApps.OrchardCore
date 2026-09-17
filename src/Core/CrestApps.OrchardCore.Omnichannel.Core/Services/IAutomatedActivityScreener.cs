using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Asked whether an automated activity may still be placed, immediately before it is.
/// </summary>
/// <remarks>
/// A batch decides who to contact when it is loaded, and the activities it creates are placed later — often hours
/// later, and later again if they are rescheduled. Everything that could have changed in between is this
/// question: the person may have asked to be left alone, their number may now be on a national registry, or the
/// hour may no longer be one they can lawfully be called at.
/// <para>
/// An abstraction rather than a direct call because the omnichannel module knows nothing of telephony or of
/// do-not-call registries, and must keep running on a tenant that has neither. Screeners are resolved optionally:
/// none registered means nothing to ask, and the activity proceeds.
/// </para>
/// <para>
/// Screeners are asked in registration order and the first refusal wins. A screener that cannot answer — a
/// registry that is unreachable, a number it cannot canonicalize — is expected to refuse rather than allow,
/// because a call that should not have been placed cannot be taken back.
/// </para>
/// </remarks>
public interface IAutomatedActivityScreener
{
    /// <summary>
    /// The channel this screener has an opinion about, so an SMS is not screened by a voice rule.
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Whether this activity may be placed now.
    /// </summary>
    /// <param name="activity">The activity about to be placed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AutomatedActivityScreeningResult> ScreenAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a screener decided, and why.
/// </summary>
public sealed class AutomatedActivityScreeningResult
{
    /// <summary>
    /// Gets a value indicating whether the activity may be placed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Gets the short reason it may not be, for the record on the activity.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets the human-readable explanation of the refusal.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// The activity may be placed.
    /// </summary>
    public static AutomatedActivityScreeningResult Allow()
        => new() { IsAllowed = true };

    /// <summary>
    /// The activity may not be placed.
    /// </summary>
    /// <param name="reason">A short, stable reason code.</param>
    /// <param name="description">What to tell whoever reads the activity later.</param>
    public static AutomatedActivityScreeningResult Deny(string reason, string description)
        => new() { IsAllowed = false, Reason = reason, Description = description };
}
