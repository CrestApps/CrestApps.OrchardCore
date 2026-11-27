using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;

/// <summary>
/// The create view model for a broadcast.
/// </summary>
public class BroadcastCreateViewModel
{
    /// <summary>
    /// Gets or sets the broadcast name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the sending channel endpoint, which decides the channel.
    /// </summary>
    public string EndpointId { get; set; }

    /// <summary>
    /// Gets or sets the subject line, sent when the channel of the endpoint picked supports one.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the recipient addresses, one per line or comma-separated.
    /// </summary>
    public string RecipientsText { get; set; }

    /// <summary>
    /// Gets or sets the addresses selected through the contact picker.
    /// </summary>
    public IList<string> ContactAddresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the selectable endpoints of every enabled messaging channel.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Endpoints { get; set; }

    /// <summary>
    /// Gets or sets the channel of each endpoint, keyed by endpoint id.
    /// </summary>
    [BindNever]
    public IReadOnlyDictionary<string, string> EndpointChannels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the channels whose messages carry a subject.
    /// </summary>
    [BindNever]
    public IReadOnlyCollection<string> SubjectChannels { get; set; } = [];

    /// <summary>
    /// Gets or sets the editor shape, built by the display drivers of this view model, which groups the fields in cards.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }
}

/// <summary>
/// The list view model for broadcasts.
/// </summary>
public class BroadcastListViewModel
{
    /// <summary>
    /// Gets or sets the broadcasts, most-recent first, the admin list renders.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// A broadcast's progress as a row of the admin list shows it: the run's counts with what the outbox did with the
/// messages the channel held back.
/// </summary>
public class BroadcastProgressViewModel
{
    /// <summary>
    /// Gets or sets the broadcast.
    /// </summary>
    public CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models.MessagingBroadcast Broadcast { get; set; }

    /// <summary>
    /// Gets or sets the status shown: a completed broadcast with messages still queued is still sending.
    /// </summary>
    public CrestApps.OrchardCore.Omnichannel.Messaging.Models.MessagingBroadcastStatus Status { get; set; }

    /// <summary>
    /// Gets or sets how many recipients the message was actually sent to.
    /// </summary>
    public int SentCount { get; set; }

    /// <summary>
    /// Gets or sets how many held-back messages still wait in the outbox.
    /// </summary>
    public int QueuedCount { get; set; }

    /// <summary>
    /// Gets or sets how many recipients the message could not be sent to.
    /// </summary>
    public int FailedCount { get; set; }
}