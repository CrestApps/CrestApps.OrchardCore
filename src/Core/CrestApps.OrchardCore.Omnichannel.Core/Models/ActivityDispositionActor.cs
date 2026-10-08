namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Identifies who or what dispositioned (completed) an activity, as recorded on
/// <see cref="OmnichannelActivity.DispositionedBy"/> so the activity screens can say how it ended.
/// </summary>
/// <remarks>
/// This is deliberately coarser than <see cref="ActivityDispositionSource"/>, which describes the request and is
/// published to reporting: it answers the single question a person reading a completed activity asks — was it a
/// person, the AI agent, the dialer, or the platform itself. The values are stored, so new members are appended.
/// </remarks>
public enum ActivityDispositionActor
{
    /// <summary>
    /// Not recorded. Activities completed before the actor was stored read as this value; use
    /// <see cref="ActivityDispositionActors.Resolve(OmnichannelActivity)"/> to infer the actor for them.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A person (an agent or an administrator) selected the disposition.
    /// </summary>
    User = 1,

    /// <summary>
    /// The AI agent that handled the automated conversation concluded it.
    /// </summary>
    AIAgent = 2,

    /// <summary>
    /// The dialer dispositioned the attempt on its own, for example after no answer, a busy line, an answering
    /// machine, an abandoned call or a number that is not in service.
    /// </summary>
    Dialer = 3,

    /// <summary>
    /// A platform process completed the activity without a person, the AI agent or the dialer deciding it, such as
    /// an IVR transfer, a callback taking over the call, or a workflow.
    /// </summary>
    System = 4,
}
