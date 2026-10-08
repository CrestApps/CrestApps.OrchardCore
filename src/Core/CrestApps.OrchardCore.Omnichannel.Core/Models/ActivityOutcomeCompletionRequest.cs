namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// A request to complete an activity with the disposition for an outcome the platform reached on its own.
/// </summary>
public sealed class ActivityOutcomeCompletionRequest
{
    /// <summary>
    /// Gets or sets the activity to complete.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the outcome the disposition must stand for.
    /// </summary>
    public DispositionOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the outcome to fall back to when no disposition stands for <see cref="Outcome"/>, or
    /// <see cref="DispositionOutcome.None"/> for none.
    /// </summary>
    public DispositionOutcome FallbackOutcome { get; set; } = DispositionOutcome.NoAnswer;

    /// <summary>
    /// Gets or sets whether the disposition the subject's "Try again" action is wired to may be used when the subject's
    /// flow has no disposition for either outcome, so the contact is still tried again.
    /// </summary>
    public bool PreferRetriedDisposition { get; set; } = true;

    /// <summary>
    /// Gets or sets the terminal reason recorded on the completed activity, so a report can tell why it ended.
    /// </summary>
    public string TerminalReasonCode { get; set; }

    /// <summary>
    /// Gets or sets the notes added to the activity.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets who reached the outcome.
    /// </summary>
    public ActivityDispositionSource Source { get; set; } = ActivityDispositionSource.System;

    /// <summary>
    /// Gets or sets who dispositioned the activity, recorded on <see cref="OmnichannelActivity.DispositionedBy"/>.
    /// Leave it unset to derive it from <see cref="Source"/>.
    /// </summary>
    public ActivityDispositionActor? DispositionedBy { get; set; }
}
