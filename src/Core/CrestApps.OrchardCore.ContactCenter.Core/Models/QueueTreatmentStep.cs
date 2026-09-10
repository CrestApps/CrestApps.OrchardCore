namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What kind of thing the caller should hear next.
/// </summary>
public enum QueueTreatmentStepKind
{
    /// <summary>
    /// Nothing is due.
    /// </summary>
    None,

    /// <summary>
    /// The one-time greeting.
    /// </summary>
    Welcome,

    /// <summary>
    /// The periodic update, optionally carrying position and estimated wait.
    /// </summary>
    Announcement,

    /// <summary>
    /// The offer to be called back instead of waiting.
    /// </summary>
    CallbackOffer,

    /// <summary>
    /// Start the hold music, for a queue that plays music but has no greeting to start it behind.
    /// </summary>
    HoldMusic,
}

/// <summary>
/// The next thing to play to a waiting caller.
/// </summary>
/// <param name="Kind">What kind of step this is.</param>
/// <param name="Text">The text to speak, when there is any.</param>
/// <param name="DtmfKey">The key the caller presses to accept, for a callback offer.</param>
public readonly record struct QueueTreatmentStep(QueueTreatmentStepKind Kind, string Text, string DtmfKey)
{
    /// <summary>
    /// Nothing is due for this caller right now.
    /// </summary>
    public static QueueTreatmentStep None { get; } = new(QueueTreatmentStepKind.None, null, null);
}
