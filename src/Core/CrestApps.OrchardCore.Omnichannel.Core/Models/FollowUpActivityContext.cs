namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// The activity that tries a contact again, as it is being created from a completed one.
/// </summary>
public sealed class FollowUpActivityContext
{
    /// <summary>
    /// Gets or sets the completed activity the follow-up repeats.
    /// </summary>
    public OmnichannelActivity PreviousActivity { get; set; }

    /// <summary>
    /// Gets or sets the follow-up activity, not yet saved. A handler may change it.
    /// </summary>
    public OmnichannelActivity FollowUpActivity { get; set; }

    /// <summary>
    /// Gets or sets what is creating the follow-up: a subject action type such as
    /// <see cref="OmnichannelConstants.ActionTypes.TryAgain"/>, or the name of the workflow task.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets whether whatever created the follow-up named the person who owns it. Such a follow-up is that
    /// person's own work rather than one for the system that carried the first attempt.
    /// </summary>
    public bool HasNamedOwner { get; set; }

    /// <summary>
    /// Gets or sets whether a handler refused the follow-up, so it is not created.
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Gets or sets why the follow-up was refused.
    /// </summary>
    public string CancelReason { get; set; }
}
