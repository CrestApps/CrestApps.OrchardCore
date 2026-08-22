using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Telephony.Sms.ViewModels;

/// <summary>
/// The view model for starting a new conversation from the portal.
/// </summary>
public class SmsComposeViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the sending SMS channel endpoint (DID).
    /// </summary>
    public string EndpointId { get; set; }

    /// <summary>
    /// Gets or sets the recipient number (E.164).
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the selectable SMS channel endpoints (DIDs).
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Endpoints { get; set; }
}

/// <summary>
/// The inbox list view model: the conversations the current agent can see, each rendered through the display
/// manager so drivers can extend the row.
/// </summary>
public class SmsInboxViewModel
{
    /// <summary>
    /// Gets or sets the rendered conversation rows.
    /// </summary>
    public IList<SmsInboxRow> Rows { get; set; } = [];
}

/// <summary>
/// A single inbox row: the conversation and its rendered summary shape.
/// </summary>
public class SmsInboxRow
{
    /// <summary>
    /// Gets or sets the conversation.
    /// </summary>
    public SmsConversation Conversation { get; set; }

    /// <summary>
    /// Gets or sets the rendered summary shape.
    /// </summary>
    public IShape Shape { get; set; }
}

/// <summary>
/// The conversation thread view model: the conversation, its ordered message bubbles, and the composer state.
/// </summary>
public class SmsThreadViewModel
{
    /// <summary>
    /// Gets or sets the conversation.
    /// </summary>
    public SmsConversation Conversation { get; set; }

    /// <summary>
    /// Gets or sets the ordered messages in the thread.
    /// </summary>
    public IReadOnlyList<OmnichannelMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the composer body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the enabled canned-response templates offered in the composer.
    /// </summary>
    public IReadOnlyList<SmsTemplate> Templates { get; set; } = [];
}
