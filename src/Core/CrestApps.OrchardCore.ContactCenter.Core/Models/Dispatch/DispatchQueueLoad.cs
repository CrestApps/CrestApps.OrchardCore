namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

/// <summary>
/// How a campaign queue is being paced, which depends on the dialer profile stamped on the item at its head.
/// </summary>
public enum DispatchPacing
{
    /// <summary>
    /// Nothing is waiting, so there is nothing to pace.
    /// </summary>
    NothingWaiting,

    /// <summary>
    /// The head item's dialer profile is turned on and paced by the system (Power, Progressive or a paced Predictive profile).
    /// </summary>
    Paced,

    /// <summary>
    /// The head item's dialer profile is turned off, so the pacer skips the queue and its items are held back.
    /// </summary>
    ProfileDisabled,

    /// <summary>
    /// The head item's dialer profile is not paced by the system (Preview or Manual): agents work its records.
    /// </summary>
    AgentDriven,

    /// <summary>
    /// The head item carries no dialer profile, or one that no longer exists, so the pacer never runs for the queue.
    /// </summary>
    ProfileMissing,
}

/// <summary>
/// A dialer profile stamped on waiting queue items, with how many carry it.
/// </summary>
public sealed class DispatchProfileStamp
{
    /// <summary>
    /// Gets or sets the stamped profile identifier, or <see langword="null"/> for items stamped with none.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the profile name, or <see langword="null"/> when the profile no longer exists or the item is a queued
    /// callback.
    /// </summary>
    public string ProfileName { get; set; }

    /// <summary>
    /// Gets or sets the profile's dialing mode name, or <see langword="null"/> when the profile cannot be read.
    /// </summary>
    public string Mode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the profile is turned on. It is <see langword="null"/> when the profile
    /// cannot be read.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Gets or sets the number of waiting items stamped with the profile.
    /// </summary>
    public int ItemCount { get; set; }
}

/// <summary>
/// Pending activities that share a source, interaction type and channel.
/// </summary>
public sealed class DispatchActivityGroup
{
    /// <summary>
    /// Gets or sets the activity source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the interaction type name, such as Manual or Automated.
    /// </summary>
    public string InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the number of pending activities.
    /// </summary>
    public int Count { get; set; }
}
