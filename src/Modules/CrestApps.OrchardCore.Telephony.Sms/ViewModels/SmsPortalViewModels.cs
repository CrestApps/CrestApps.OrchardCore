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
    /// Gets or sets the recipient numbers (E.164), one per line or comma-separated. A single recipient starts a
    /// 1:1 conversation; multiple recipients start a broadcast (individual 1:1 threads).
    /// </summary>
    public string Recipients { get; set; }

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
/// A customer search result for the compose picker.
/// </summary>
public class SmsCustomerSearchResult
{
    /// <summary>
    /// Gets or sets the contact content item id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the contact display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the contact's primary phone number (E.164).
    /// </summary>
    public string Phone { get; set; }
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

    /// <summary>
    /// Gets or sets the display name of the linked customer, when the conversation resolves to a contact.
    /// </summary>
    public string ContactDisplayText { get; set; }
}
