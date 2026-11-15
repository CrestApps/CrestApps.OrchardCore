using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// The requester's list of requests.
/// </summary>
public class TicketPortalListViewModel
{
    public List<TicketPortalRow> Items { get; set; } = [];

    public bool Organization { get; set; }

    public string AccountName { get; set; }

    public bool CanCreate { get; set; }

    public int Page { get; set; }

    public int Total { get; set; }

    public int PageSize { get; set; }
}

/// <summary>
/// A request in the requester's list.
/// </summary>
public class TicketPortalRow
{
    public string Number { get; set; }

    public string Subject { get; set; }

    public string Status { get; set; }

    public bool IsOpen { get; set; }

    public string Created { get; set; }

    public string RequesterName { get; set; }
}

/// <summary>
/// The form a requester opens a request with.
/// </summary>
public class TicketPortalCreateViewModel
{
    public string ContentType { get; set; }

    public string DeskId { get; set; }

    public string Body { get; set; }

    [BindNever]
    public List<TicketPortalType> Types { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Desks { get; set; } = [];

    [BindNever]
    public IShape Editor { get; set; }

    [BindNever]
    public bool CanAttach { get; set; }
}

/// <summary>
/// A ticket type requesters may submit.
/// </summary>
public class TicketPortalType
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }
}

/// <summary>
/// A request and its conversation, as its requester sees it.
/// </summary>
public class TicketPortalDisplayViewModel
{
    public string TicketId { get; set; }

    public string Number { get; set; }

    public string Subject { get; set; }

    public string Status { get; set; }

    public string Created { get; set; }

    public List<TicketPortalEntry> Entries { get; set; } = [];

    public bool CanReply { get; set; }

    public bool CanAttach { get; set; }

    /// <summary>
    /// Gets or sets the guest's token, when the request is opened from a signed link.
    /// </summary>
    public bool IsGuest { get; set; }

    /// <summary>
    /// Gets or sets what was just done, such as a reply sent.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets why the last action was refused.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the reply the requester wrote, kept after a refusal.
    /// </summary>
    public string Draft { get; set; }

    /// <summary>
    /// Gets or sets how many messages between the original request and the latest ones are left out.
    /// </summary>
    public int Skipped { get; set; }

    public bool CanRate { get; set; }

    public int? Score { get; set; }

    public string RatingComment { get; set; }
}

/// <summary>
/// The page a requester rates a ticket on.
/// </summary>
public class TicketPortalRateViewModel
{
    public string TicketId { get; set; }

    public string Number { get; set; }

    public string Subject { get; set; }

    public int? Score { get; set; }

    public string Comment { get; set; }

    public bool Rated { get; set; }

    public bool Thanked { get; set; }

    /// <summary>
    /// Gets or sets why the rating wasn't saved.
    /// </summary>
    public string Error { get; set; }
}

/// <summary>
/// A message on a request: the requester's or an agent's public reply.
/// </summary>
public class TicketPortalEntry
{
    public string AuthorName { get; set; }

    public bool IsRequester { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer wrote it.
    /// </summary>
    public bool IsMine { get; set; }

    public string When { get; set; }

    public string Body { get; set; }

    public List<TicketPortalFile> Files { get; set; } = [];
}

/// <summary>
/// A file on a message, with a download link that works for an hour.
/// </summary>
public class TicketPortalFile
{
    public string FileName { get; set; }

    public string Url { get; set; }
}
