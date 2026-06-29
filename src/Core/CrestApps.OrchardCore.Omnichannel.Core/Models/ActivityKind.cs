namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Identifies the kind of work represented by an omnichannel activity.
/// </summary>
public enum ActivityKind
{
    /// <summary>
    /// A general task activity.
    /// </summary>
    Task,

    /// <summary>
    /// A phone-call activity.
    /// </summary>
    Call,

    /// <summary>
    /// An email activity.
    /// </summary>
    Email,

    /// <summary>
    /// An SMS activity.
    /// </summary>
    Sms,

    /// <summary>
    /// A meeting activity.
    /// </summary>
    Meeting,
}
