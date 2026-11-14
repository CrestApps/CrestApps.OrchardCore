using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A reply or an internal note on a ticket.
/// </summary>
public sealed class TicketReply
{
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets whether it is an internal note rather than a public reply.
    /// </summary>
    public bool IsInternal { get; set; }

    /// <summary>
    /// Gets or sets who wrote it.
    /// </summary>
    public PartyRef Author { get; set; }

    /// <summary>
    /// Gets or sets the author's name, for an author no party source knows, such as a guest who wrote by e-mail.
    /// </summary>
    public string AuthorName { get; set; }

    /// <summary>
    /// Gets or sets whether the requester wrote it rather than an agent.
    /// </summary>
    public bool FromRequester { get; set; }

    /// <summary>
    /// Gets or sets where it came in or goes out, such as <c>Email</c>.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets its files.
    /// </summary>
    public List<string> AttachmentIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the status to set with it, such as solved; <see langword="null"/> leaves the status to the rules.
    /// </summary>
    public string SetStatusId { get; set; }

    /// <summary>
    /// Gets or sets the channel's delivery data, such as the message id, kept on the entry.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = [];
}
