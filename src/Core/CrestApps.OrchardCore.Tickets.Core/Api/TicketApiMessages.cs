using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Timeline.Models;

namespace CrestApps.OrchardCore.Tickets.Core.Api;

/// <summary>
/// A page of tickets from the tickets API.
/// </summary>
/// <param name="Items">The tickets.</param>
/// <param name="Total">How many tickets match in all.</param>
/// <param name="Skip">How many were skipped.</param>
/// <param name="Take">How many were asked for.</param>
public sealed record TicketApiPage(IReadOnlyList<TicketApiModel> Items, int Total, int Skip, int Take)
{
    /// <summary>
    /// The most tickets one page holds.
    /// </summary>
    public const int MaxTake = 100;

    /// <summary>
    /// The page size when none is asked for.
    /// </summary>
    public const int DefaultTake = 25;

    public static int ClampSkip(int? skip) => Math.Max(skip ?? 0, 0);

    public static int ClampTake(int? take) => Math.Clamp(take ?? DefaultTake, 1, MaxTake);
}

/// <summary>
/// A file sent with a new ticket, a reply or a note, its content in base64.
/// </summary>
public sealed class TicketApiFile
{
    public string FileName { get; set; }

    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file's bytes, sent as a base64 string.
    /// </summary>
    public byte[] Content { get; set; }
}

/// <summary>
/// A ticket to open through the API.
/// </summary>
public sealed class TicketApiCreateRequest
{
    /// <summary>
    /// Gets or sets the ticket type (a content type with the ticket part); the default type when empty.
    /// </summary>
    public string Type { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    public string DeskId { get; set; }

    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the requester as <c>Kind:Id</c>, such as <c>User:…</c> or <c>Contact:…</c>.
    /// </summary>
    public string Requester { get; set; }

    /// <summary>
    /// Gets or sets the requester's name, for a requester who isn't a known party.
    /// </summary>
    public string RequesterName { get; set; }

    /// <summary>
    /// Gets or sets the requester's e-mail, for a requester who isn't a known party.
    /// </summary>
    public string RequesterEmail { get; set; }

    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the category, a term of the ticket type's category taxonomy.
    /// </summary>
    public string CategoryId { get; set; }

    public List<TicketApiFile> Attachments { get; set; } = [];
}

/// <summary>
/// Changes to a ticket through the API. A value left out stays as it is; an empty team or assignee clears it.
/// </summary>
public sealed class TicketApiUpdateRequest
{
    public string Subject { get; set; }

    public string StatusId { get; set; }

    public string PriorityId { get; set; }

    public string DeskId { get; set; }

    public string TeamId { get; set; }

    public string AssigneeId { get; set; }

    public int? EscalationLevel { get; set; }

    /// <summary>
    /// Gets or sets the new category; an empty string clears it. Only the category changes, not the desk or priority.
    /// </summary>
    public string CategoryId { get; set; }
}

/// <summary>
/// A public reply or an internal note added through the API.
/// </summary>
public sealed class TicketApiReplyRequest
{
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets a status to move the ticket to with the reply; public replies only.
    /// </summary>
    public string SetStatusId { get; set; }

    public List<TicketApiFile> Attachments { get; set; } = [];
}

/// <summary>
/// A reply, note or event on a ticket, as the API gives it.
/// </summary>
public sealed class TicketApiEntry
{
    public string Id { get; init; }

    public string Kind { get; init; }

    public bool IsInternal { get; init; }

    public string Body { get; init; }

    public string Author { get; init; }

    public string AuthorName { get; init; }

    public string Channel { get; init; }

    public DateTime CreatedUtc { get; init; }

    public bool Redacted { get; init; }

    public IReadOnlyList<TicketApiChange> Changes { get; init; } = [];

    public IReadOnlyList<TicketApiAttachment> Attachments { get; init; } = [];

    /// <summary>
    /// Describes an entry and its files.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="attachments">The entry's files.</param>
    /// <param name="urlOf">Gives a file's download address.</param>
    /// <returns>The API entry.</returns>
    public static TicketApiEntry From(TimelineEntry entry, IEnumerable<Attachment> attachments, Func<Attachment, string> urlOf)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var redacted = entry.RedactedUtc is not null;

        return new TicketApiEntry
        {
            Id = entry.ItemId,
            Kind = entry.Kind,
            IsInternal = entry.Visibility == TimelineVisibility.Internal,
            Body = redacted ? null : entry.Body,
            Author = entry.Author.IsEmpty ? null : entry.Author.ToString(),
            AuthorName = entry.AuthorName,
            Channel = entry.Channel,
            CreatedUtc = entry.CreatedUtc,
            Redacted = redacted,
            Changes = [.. (entry.Changes ?? []).Select(change => new TicketApiChange { Field = change.Field, From = change.From, To = change.To })],
            Attachments = redacted
                ? []
                : [.. (attachments ?? []).Select(attachment => new TicketApiAttachment
                {
                    Id = attachment.ItemId,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    Length = attachment.Length,
                    Url = urlOf?.Invoke(attachment),
                })],
        };
    }
}

/// <summary>
/// A field change recorded on a ticket entry.
/// </summary>
public sealed class TicketApiChange
{
    public string Field { get; init; }

    public string From { get; init; }

    public string To { get; init; }
}

/// <summary>
/// A file of a ticket entry, with a download address that works for a while.
/// </summary>
public sealed class TicketApiAttachment
{
    public string Id { get; init; }

    public string FileName { get; init; }

    public string ContentType { get; init; }

    public long Length { get; init; }

    public string Url { get; init; }
}
