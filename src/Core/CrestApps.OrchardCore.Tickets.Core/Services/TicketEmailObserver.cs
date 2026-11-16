using System.Net;
using System.Text;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// E-mails the requester when their ticket is received, when an agent replies in public, and when it is solved, on a
/// desk that e-mails requesters. Each e-mail is a templated e-mail of the Tickets group, so the site can write its own
/// (<see cref="TicketEmailSettings"/>). It goes once the request's work is committed, so a change that is rolled back
/// sends nothing, and a mail server that is down never undoes the change.
/// </summary>
public sealed class TicketEmailObserver : ITicketObserver
{
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IPartyDirectory _parties;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketEmailObserver"/> class.
    /// </summary>
    /// <param name="desks">The desks.</param>
    /// <param name="parties">The party directory, for a requester's e-mail.</param>
    /// <param name="sender">The templated e-mail sender, used directly when there's no shell scope to defer to.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketEmailObserver(ICatalog<TicketDesk> desks, IPartyDirectory parties, IServiceProvider services, IStringLocalizer<TicketEmailObserver> stringLocalizer)
    {
        _desks = desks;
        _parties = parties;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        if (await RecipientAsync(ticket, cancellationToken) is not { } to)
        {
            return;
        }

        var model = TicketEmailModel.From(ticket, null, await LinkAsync(ticket, cancellationToken), await TicketEmailModel.CategoryAsync(ticket, _services.GetService<ITicketCategoryService>(), cancellationToken));
        var subject = model.Subject is null ? S["We received your request {0}", model.Number].Value : S["We received your request: {0}", model.Subject].Value;
        var html = $"<p>{Encode(S["We received your request and gave it the number {0}. We'll get back to you soon.", model.Number])}</p>"
            + Strong(model.Subject)
            + ViewLink(model.Link);

        await SendAsync(TicketsConstants.EmailKinds.Received, to, model, null, subject, html, null, ticket.ContentItemId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RepliedAsync(ContentItem ticket, TimelineEntry entry, bool fromRequester, CancellationToken cancellationToken = default)
    {
        // Only what an agent writes to the requester; notes and the requester's own words aren't e-mailed back.
        if (fromRequester || entry is null || entry.Visibility != TimelineVisibility.Public || string.IsNullOrWhiteSpace(entry.Body)
            || await RecipientAsync(ticket, cancellationToken) is not { } to)
        {
            return;
        }

        var model = TicketEmailModel.From(ticket, null, await LinkAsync(ticket, cancellationToken), await TicketEmailModel.CategoryAsync(ticket, _services.GetService<ITicketCategoryService>(), cancellationToken));
        var files = await FilesAsync(entry, cancellationToken);
        var reply = new TicketEmailReply { Body = entry.Body, Html = Paragraphs(entry.Body) + files, AuthorName = entry.AuthorName };
        var subject = $"[{model.Number}] {model.Subject ?? S["Your request"].Value}";
        var html = reply.Html
            + $"<hr><p>{Encode(model.Subject is null ? S["Request {0}", model.Number] : S["Request {0}: {1}", model.Number, model.Subject])}</p>"
            + ViewLink(model.Link);

        await SendAsync(TicketsConstants.EmailKinds.Reply, to, model, reply, subject, html, entry.ItemId, ticket.ContentItemId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StatusChangedAsync(ContentItem ticket, TicketStatus previous, TicketStatus current, CancellationToken cancellationToken = default)
    {
        if (current?.Category != TicketStatusCategory.Solved || previous?.Category == TicketStatusCategory.Solved
            || await RecipientAsync(ticket, cancellationToken) is not { } to)
        {
            return;
        }

        var model = TicketEmailModel.From(ticket, current, await LinkAsync(ticket, cancellationToken), await TicketEmailModel.CategoryAsync(ticket, _services.GetService<ITicketCategoryService>(), cancellationToken));
        var subject = S["Solved: {0}", model.Subject ?? model.Number].Value;
        var html = $"<p>{Encode(S["Your request {0} is solved.", model.Number])}</p>"
            + Strong(model.Subject)
            + $"<p>{Encode(S["If it isn't, contact us and give the number {0}.", model.Number])}</p>"
            + ViewLink(model.Link);

        await SendAsync(TicketsConstants.EmailKinds.Solved, to, model, null, subject, html, null, ticket.ContentItemId, cancellationToken);
    }

    private static string Strong(string text)
        => string.IsNullOrEmpty(text) ? null : $"<p><strong>{Encode(text)}</strong></p>";

    // The requester's e-mail, on a desk that e-mails requesters.
    private async Task<string> RecipientAsync(ContentItem ticket, CancellationToken cancellationToken)
    {
        return ticket.TryGet<TicketPart>(out var part)
            ? await TicketRequesterEmail.FindAsync(part, await _desks.FindByIdAsync(part.DeskId, cancellationToken), _parties, cancellationToken)
            : null;
    }

    // The e-mail is kept in the outbox and tried at once; one that fails is tried again later, and the reply shows it.
    private async Task SendAsync(string kind, string to, TicketEmailModel model, TicketEmailReply reply, string subject, string html, string entryId, string ticketId, CancellationToken cancellationToken)
        => await _services.GetRequiredService<TicketEmailOutbox>().SendAsync(new TicketEmailOutboxItem
        {
            TicketId = ticketId,
            EntryId = entryId,
            Kind = kind,
            To = to,
            Reference = model.Number,
            Subject = subject,
            Html = html,
            TicketNumber = model.Number,
            TicketSubject = model.Subject,
            TicketCategory = model.Category,
            TicketStatus = model.Status,
            RequesterName = model.RequesterName,
            ReplyBody = reply?.Body,
            ReplyHtml = reply?.Html,
            ReplyAuthorName = reply?.AuthorName,
            TicketLink = model.Link,
        }, cancellationToken);

    // The link to the request on the site (with the Tickets Portal feature), or none.
    private async Task<string> LinkAsync(ContentItem ticket, CancellationToken cancellationToken)
        => _services.GetService<ITicketRequesterLinks>() is { } links ? await links.GetLinkAsync(ticket, cancellationToken) : null;

    private string ViewLink(string link)
        => string.IsNullOrEmpty(link) ? string.Empty : $"<p><a href=\"{Encode(link)}\">{Encode(S["View your request"])}</a></p>";

    // Files of a public reply reach the requester as download links that work for 30 days, from the site's own address.
    private async Task<string> FilesAsync(TimelineEntry entry, CancellationToken cancellationToken)
    {
        if (entry.AttachmentIds is not { Count: > 0 } || _services.GetService<IAttachmentService>() is not { } attachments)
        {
            return string.Empty;
        }

        var files = await attachments.GetAsync(entry.AttachmentIds, cancellationToken);
        var baseUrl = (await _services.GetRequiredService<ISiteService>().GetSiteSettingsAsync()).BaseUrl?.TrimEnd('/');
        var html = new StringBuilder("<p>").Append(Encode(S["Files:"])).Append("</p><ul>");

        foreach (var file in files)
        {
            var path = attachments.CreateSignedPath(file.ItemId, TimeSpan.FromDays(30));

            // Without the site's address the link can't be opened from an e-mail; the file is named instead.
            html.Append("<li>").Append(string.IsNullOrEmpty(baseUrl)
                ? Encode(S["{0} (open the request to download it)", file.FileName])
                : $"<a href=\"{Encode(OriginOf(baseUrl) + path)}\">{Encode(file.FileName)}</a>").Append("</li>");
        }

        return html.Append("</ul>").ToString();
    }

    // The signed path already carries the tenant's prefix, so only the site address's scheme and host are added.
    private static string OriginOf(string baseUrl)
        => Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : baseUrl;

    private static string Encode(string text)
        => WebUtility.HtmlEncode(text ?? string.Empty);

    private static string Paragraphs(string text)
    {
        var html = new StringBuilder();

        foreach (var paragraph in (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            html.Append("<p>").Append(Encode(paragraph.Trim()).Replace("\n", "<br>", StringComparison.Ordinal)).Append("</p>");
        }

        return html.ToString();
    }
}

/// <summary>
/// A ticket as an e-mail template reads it: <c>{{ Ticket.Number }}</c>, <c>{{ Ticket.Subject }}</c>,
/// <c>{{ Ticket.Status }}</c> (the status's label for customers), <c>{{ Ticket.RequesterName }}</c>,
/// <c>{{ Ticket.Category }}</c> (the category with those above it, such as "Hardware > Laptop").
/// </summary>
public sealed class TicketEmailModel
{
    /// <summary>
    /// Gets the ticket's number.
    /// </summary>
    public string Number { get; init; }

    /// <summary>
    /// Gets the ticket's subject.
    /// </summary>
    public string Subject { get; init; }

    /// <summary>
    /// Gets the status as customers see it, if known.
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Gets the requester's name, if known.
    /// </summary>
    public string RequesterName { get; init; }

    /// <summary>
    /// Gets the ticket's category with the categories above it, such as "Hardware > Laptop", if it has one.
    /// </summary>
    public string Category { get; init; }

    /// <summary>
    /// Gets the link to the request on the site (their account page, or a signed link for a guest), when the Tickets
    /// Portal feature is on and the site has an address.
    /// </summary>
    public string Link { get; init; }

    /// <summary>
    /// Builds the model of a ticket.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="status">Its status, if known.</param>
    /// <returns>The model.</returns>
    /// <param name="link">The link to the request on the site, if any.</param>
    /// <param name="category">The ticket's category path, by names, if any (<see cref="CategoryAsync"/>).</param>
    public static TicketEmailModel From(ContentItem ticket, TicketStatus status, string link = null, string category = null)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        ticket.TryGet<TicketPart>(out var part);

        // To an unconfirmed address (a guest typed it), nothing the guest wrote: not the subject, not the name, and no
        // link to a page that shows them. Templates then get those empty.
        var unverified = part?.RequesterUnverified == true;

        return new TicketEmailModel
        {
            Number = part?.Number,
            Subject = unverified ? null : ticket.DisplayText,
            Status = status is null ? null : string.IsNullOrWhiteSpace(status.CustomerLabel) ? status.Name : status.CustomerLabel,
            RequesterName = unverified ? null : part?.RequesterContact?.Name,
            Link = unverified ? null : link,

            // The site's own words (a category of its taxonomy), so a guest's ticket shows it too.
            Category = category,
        };
    }

    /// <summary>
    /// Finds a ticket's category with the categories above it, such as "Hardware > Laptop", for its e-mails.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="categories">The category service, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The category's path by names, or <see langword="null"/>.</returns>
    public static async Task<string> CategoryAsync(ContentItem ticket, ITicketCategoryService categories, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (!ticket.TryGet<TicketPart>(out var part)
            || string.IsNullOrEmpty(part.CategoryId)
            || categories is null)
        {
            return null;
        }

        return (await categories.FindAsync(ticket.ContentType, part.CategoryId, cancellationToken))?.PathText;
    }
}

/// <summary>
/// An agent's reply as an e-mail template reads it: <c>{{ Reply.Body }}</c> and <c>{{ Reply.AuthorName }}</c>.
/// </summary>
public sealed class TicketEmailReply
{
    /// <summary>
    /// Gets the reply as HTML, already encoded, with its paragraphs and line breaks and links to its files. Write it raw
    /// in a template: <c>{{ Reply.Html | raw }}</c>. <c>Reply.Body</c> is the plain text, encoded where it is written.
    /// </summary>
    public string Html { get; init; }

    /// <summary>
    /// Gets the reply's text.
    /// </summary>
    public string Body { get; init; }

    /// <summary>
    /// Gets the name of the agent who wrote it, if known.
    /// </summary>
    public string AuthorName { get; init; }
}
