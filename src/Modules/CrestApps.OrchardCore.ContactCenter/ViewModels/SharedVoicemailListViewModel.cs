using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// The shared voicemail page: the messages in the boxes of the queues the viewer may see, and the filters over them.
/// </summary>
public class SharedVoicemailListViewModel
{
    /// <summary>
    /// Gets or sets the queue the list is filtered to, or <see langword="null"/> for every queue the viewer may see.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets which messages the list shows by their handling.
    /// </summary>
    public SharedVoicemailStatusFilter Status { get; set; }

    /// <summary>
    /// Gets or sets the queues the viewer may filter by.
    /// </summary>
    public IList<SelectListItem> QueueOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the messages on this page.
    /// </summary>
    public IList<SharedVoicemailItemViewModel> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of messages nobody has claimed yet, across every queue the viewer may see.
    /// </summary>
    public int NewCount { get; set; }

    /// <summary>
    /// Gets or sets the pager shape rendered beneath the list.
    /// </summary>
    public object Pager { get; set; }
}

/// <summary>
/// One message on the shared voicemail page, with what the viewer may do with it.
/// </summary>
public class SharedVoicemailItemViewModel
{
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public SharedVoicemail Voicemail { get; set; }

    /// <summary>
    /// Gets or sets the name of the message's queue.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets how long the message is, in seconds, when known.
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a recording was captured for the message.
    /// </summary>
    public bool HasRecording { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer is the one handling the message.
    /// </summary>
    public bool IsMine { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may claim the message.
    /// </summary>
    public bool CanClaim { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may return the message to the team.
    /// </summary>
    public bool CanRelease { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may mark the message as dealt with.
    /// </summary>
    public bool CanResolve { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may have the caller called back.
    /// </summary>
    public bool CanCallBack { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may delete the message.
    /// </summary>
    public bool CanDelete { get; set; }
}

/// <summary>
/// Which messages the shared voicemail page shows by their handling.
/// </summary>
public enum SharedVoicemailStatusFilter
{
    /// <summary>
    /// The messages still waiting on the team: new and claimed.
    /// </summary>
    Open,

    /// <summary>
    /// The messages nobody has claimed.
    /// </summary>
    New,

    /// <summary>
    /// The messages somebody is handling.
    /// </summary>
    Claimed,

    /// <summary>
    /// The messages already dealt with.
    /// </summary>
    Resolved,

    /// <summary>
    /// Every message.
    /// </summary>
    All,
}
