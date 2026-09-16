namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What should happen to the caller next.
/// </summary>
public enum IvrStepKind
{
    /// <summary>
    /// The event described a menu the caller has already left, so nothing happens.
    /// </summary>
    Ignored,

    /// <summary>
    /// Play a menu and collect a key.
    /// </summary>
    Prompt,

    /// <summary>
    /// Send the caller to a queue.
    /// </summary>
    RouteToQueue,

    /// <summary>
    /// Send the caller to a specific agent.
    /// </summary>
    RouteToAgent,

    /// <summary>
    /// Send the caller to voicemail.
    /// </summary>
    Voicemail,

    /// <summary>
    /// Transfer the caller to an approved external destination.
    /// </summary>
    ExternalTransfer,

    /// <summary>
    /// There is no menu; route the caller the way the entry point already said to.
    /// </summary>
    Done,
}

/// <summary>
/// The next thing the caller should hear or be sent to.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="NodeId">The menu this step belongs to, for a prompt.</param>
/// <param name="Prompt">The text to speak.</param>
/// <param name="PromptMediaId">The media to play instead of speaking.</param>
/// <param name="TargetId">What to route to, for a routing step.</param>
public readonly record struct IvrStep(
    IvrStepKind Kind,
    string NodeId,
    string Prompt,
    string PromptMediaId,
    string TargetId)
{
    /// <summary>
    /// Nothing to do: the event described a menu the caller has already left.
    /// </summary>
    public static IvrStep Ignored { get; } = new(IvrStepKind.Ignored, null, null, null, null);

    /// <summary>
    /// There is no menu on this entry point.
    /// </summary>
    public static IvrStep Done { get; } = new(IvrStepKind.Done, null, null, null, null);
}
