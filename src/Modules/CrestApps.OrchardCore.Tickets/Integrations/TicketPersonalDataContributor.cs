using System.Globalization;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.Attachments.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Integrations;

/// <summary>
/// A person's tickets in a personal data request: the tickets they asked for, as a signed-in user or by the e-mail kept
/// on the ticket. An export lists each ticket and the public replies on it; an erasure removes their contact details
/// from each ticket and rewrites the subject and the timeline's text so nothing names them.
/// </summary>
internal sealed class TicketPersonalDataContributor : IPersonalDataContributor
{
    public const string TicketKind = "Ticket";

    public const string ReplyKind = "TicketReply";

    // How many tickets are read at a time.
    private const int BatchSize = 200;

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly ITimelineService _timeline;
    private readonly IServiceProvider _services;

    public TicketPersonalDataContributor(ISession session, IContentManager contentManager, ITimelineService timeline, IServiceProvider services)
    {
        _session = session;
        _contentManager = contentManager;
        _timeline = timeline;
        _services = services;
    }

    public async Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        // Only the name, for scrubbing free text: an e-mail kept on a ticket may be one several people share.
        foreach (var ticket in await ListAsync(subject, cancellationToken))
        {
            if (ticket.TryGet<TicketPart>(out var part))
            {
                subject.AddName(part.RequesterContact?.Name);
            }
        }
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var ticket in await ListAsync(context.Subject, cancellationToken))
        {
            ticket.TryGet<TicketPart>(out var part);
            part ??= new TicketPart();

            var record = context.Add(TicketKind, ticket.ContentItemId, ticket.DisplayText, ticket.CreatedUtc);
            record.Fields[nameof(TicketPart.Number)] = part.Number;
            record.Fields[nameof(TicketPart.StatusCategory)] = part.StatusCategory.ToString();
            record.Fields[nameof(TicketPart.Channel)] = part.Channel;
            record.Fields["RequesterName"] = part.RequesterContact?.Name;
            record.Fields["RequesterEmail"] = part.RequesterContact?.Email;
            record.Fields["RequesterPhone"] = part.RequesterContact?.Phone.HasValue == true ? part.RequesterContact.Phone.ToString() : null;
            record.Fields[nameof(TicketPart.SolvedUtc)] = part.SolvedUtc?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(TicketPart.ClosedUtc)] = part.ClosedUtc?.ToString("O", CultureInfo.InvariantCulture);

            // The public thread is what the person sees of the ticket; internal notes are the business's own records.
            foreach (var entry in await RepliesAsync(ticket, cancellationToken))
            {
                var reply = context.Add(ReplyKind, entry.ItemId, part.Number, entry.CreatedUtc);
                reply.Fields["TicketId"] = ticket.ContentItemId;
                reply.Fields[nameof(TimelineEntry.AuthorName)] = entry.AuthorName;
                reply.Fields[nameof(TimelineEntry.Body)] = entry.Body;
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var ticket in await ListAsync(context.Subject, cancellationToken))
        {
            ticket.TryGet<TicketPart>(out var current);
            var requester = current?.Requester ?? default;

            // Files the requester sent (on entries they wrote: theirs, or a guest's, which has no author) are deleted;
            // files an agent attached are the business's own records and stay.
            if (_services.GetService<IAttachmentService>() is { } attachments)
            {
                foreach (var entry in await RepliesAsync(ticket, cancellationToken))
                {
                    if (entry.AttachmentIds.Count > 0 && (entry.Author.IsEmpty || (!requester.IsEmpty && entry.Author == requester)))
                    {
                        foreach (var attachmentId in entry.AttachmentIds)
                        {
                            await attachments.DeleteAsync(attachmentId, cancellationToken);
                        }
                    }
                }
            }

            // The e-mails to the requester kept in the outbox hold their address and the replies; a pending one would
            // still go out to someone who asked to be forgotten.
            foreach (var email in await _session.Query<TicketEmailOutboxItem, TicketEmailOutboxIndex>(index => index.TicketId == ticket.ContentItemId).ListAsync(cancellationToken))
            {
                _session.Delete(email);
            }

            // What the requester shared under protection (their logins) goes too.
            if (_services.GetService<ITicketProtectedInformation>() is { } protectedInformation)
            {
                await protectedInformation.DeleteAllAsync(ticket.ContentItemId, cancellationToken);
            }

            ticket.Alter<TicketPart>(part =>
            {
                part.RequesterContact = part.RequesterContact is { } contact
                    ? contact with { Name = context.ReplaceName(contact.Name), Email = null, Phone = default, Address = null }
                    : null;

                // Links already e-mailed to the person stop working.
                part.AccessVersion++;
            });
            ticket.DisplayText = context.Scrub(ticket.DisplayText);

            await _contentManager.UpdateAsync(ticket);

            var entries = await _timeline.ScrubAsync(Subject(ticket), context.Scrub, cancellationToken);

            context.Result.Count(TicketKind);
            context.Result.Count(ReplyKind, entries);
        }
    }

    private async Task<List<ContentItem>> ListAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var requesters = subject.UserIds.Select(userId => new PartyRef(PartyKinds.User, userId).ToString()).ToArray();
        var emails = subject.Emails.Select(TicketIndexProvider.NormalizeEmail).Where(email => email is not null).ToArray();

        if (requesters.Length == 0 && emails.Length == 0)
        {
            return [];
        }

        // An empty IN list matches nothing.
        string[] none = ["\u0000"];
        var byRequester = requesters.Length > 0 ? requesters : none;
        var byEmail = emails.Length > 0 ? emails : none;
        var tickets = new List<ContentItem>();

        for (var skip = 0; ; skip += BatchSize)
        {
            var page = await _session.Query<ContentItem, TicketIndex>(index => index.Requester.IsIn(byRequester) || index.RequesterEmail.IsIn(byEmail))
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ListAsync(cancellationToken);

            var count = 0;

            foreach (var ticket in page)
            {
                tickets.Add(ticket);
                count++;
            }

            if (count < BatchSize)
            {
                return tickets;
            }
        }
    }

    private async Task<List<TimelineEntry>> RepliesAsync(ContentItem ticket, CancellationToken cancellationToken)
    {
        var replies = new List<TimelineEntry>();

        for (var skip = 0; ; skip += BatchSize)
        {
            var page = await _timeline.ListAsync(new TimelineQuery
            {
                Subject = Subject(ticket),
                Kinds = [TicketsConstants.EntryKinds.Reply],
                Skip = skip,
                Take = BatchSize,
            }, cancellationToken);

            replies.AddRange(page.Entries.Where(entry => entry.RedactedUtc is null));

            if (page.Entries.Count < BatchSize)
            {
                return replies;
            }
        }
    }

    private static RecordRef Subject(ContentItem ticket)
        => new(TicketsConstants.RecordKind, ticket.ContentItemId);
}
