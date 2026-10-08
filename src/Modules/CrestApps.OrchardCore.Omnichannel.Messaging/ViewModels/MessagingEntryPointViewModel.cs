using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;

/// <summary>
/// Edits what an entry point that answers a messaging channel adds: how a queue's conversations are handed out and the
/// automatic replies.
/// </summary>
public class MessagingEntryPointViewModel
{
    /// <summary>
    /// Gets or sets how conversations for a queue target are handed out.
    /// </summary>
    public ConversationDistributionMode DistributionMode { get; set; }

    /// <summary>
    /// Gets or sets the reply sent to a contact who writes in.
    /// </summary>
    public string AutoReplyMessage { get; set; }

    /// <summary>
    /// Gets or sets the reply sent instead while the entry point is closed.
    /// </summary>
    public string ClosedAutoReplyMessage { get; set; }

    /// <summary>
    /// Gets or sets the distribution modes on offer.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> DistributionModes { get; set; } = [];
}
