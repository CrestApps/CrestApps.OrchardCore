using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

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
    /// Gets or sets the number of messages nobody has claimed yet, across every queue the viewer may see.
    /// </summary>
    public int NewCount { get; set; }

    /// <summary>
    /// Gets or sets the messages on this page and the pager the <c>admin-list</c> tag renders.
    /// </summary>
    public AdminListContext List { get; set; }
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
