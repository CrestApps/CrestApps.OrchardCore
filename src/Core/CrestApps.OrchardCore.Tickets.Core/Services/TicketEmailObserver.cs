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
using OrchardCore.Environment.Shell.Scope;

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
    private readonly ITemplatedEmailSender _sender;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketEmailObserver"/> class.
    /// </summary>
    /// <param name="desks">The desks.</param>
    /// <param name="parties">The party directory, for a requester's e-mail.</param>
    /// <param name="sender">The templated e-mail sender, used directly when there's no shell scope to defer to.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketEmailObserver(ICatalog<TicketDesk> desks, IPartyDirectory parties, ITemplatedEmailSender sender, IStringLocalizer<TicketEmailObserver> stringLocalizer)
    {
        _desks = desks;
        _parties = parties;
        _sender = sender;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        if (await RecipientAsync(ticket, cancellationToken) is not { } to)
        {
            return;
        }

        var model = TicketEmailModel.From(ticket, null);
        var subject = S["We received your request: {0}", model.Subject].Value;
        var html = $"<p>{Encode(S["We received your request and gave it the number {0}. We'll get back to you soon.", model.Number])}</p>"
            + $"<p><strong>{Encode(model.Subject)}</strong></p>";

        await SendAsync(TicketsConstants.EmailKinds.Received, to, model, null, subject, html, cancellationToken);
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

        var model = TicketEmailModel.From(ticket, null);
        var reply = new TicketEmailReply { Body = entry.Body, AuthorName = entry.AuthorName };
        var subject = $"[{model.Number}] {model.Subject}";
        var html = Paragraphs(entry.Body)
            + $"<hr><p>{Encode(S["Request {0}: {1}", model.Number, model.Subject])}</p>";

        await SendAsync(TicketsConstants.EmailKinds.Reply, to, model, reply, subject, html, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StatusChangedAsync(ContentItem ticket, TicketStatus previous, TicketStatus current, CancellationToken cancellationToken = default)
    {
        if (current?.Category != TicketStatusCategory.Solved || previous?.Category == TicketStatusCategory.Solved
            || await RecipientAsync(ticket, cancellationToken) is not { } to)
        {
            return;
        }

        var model = TicketEmailModel.From(ticket, current);
        var subject = S["Solved: {0}", model.Subject].Value;
        var html = $"<p>{Encode(S["Your request {0} is solved.", model.Number])}</p>"
            + $"<p><strong>{Encode(model.Subject)}</strong></p>"
            + $"<p>{Encode(S["If it isn't, contact us and give the number {0}.", model.Number])}</p>";

        await SendAsync(TicketsConstants.EmailKinds.Solved, to, model, null, subject, html, cancellationToken);
    }

    // The requester's e-mail, on a desk that e-mails requesters.
    private async Task<string> RecipientAsync(ContentItem ticket, CancellationToken cancellationToken)
    {
        if (!ticket.TryGet<TicketPart>(out var part) || await _desks.FindByIdAsync(part.DeskId, cancellationToken) is not { NotifyRequester: true })
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(part.RequesterContact?.Email))
        {
            return part.RequesterContact.Email.Trim();
        }

        return part.Requester.IsEmpty ? null : (await _parties.GetContactInfoAsync(part.Requester, cancellationToken))?.Email;
    }

    private async Task SendAsync(string kind, string to, TicketEmailModel model, TicketEmailReply reply, string subject, string html, CancellationToken cancellationToken)
    {
        var models = new Dictionary<string, object>(StringComparer.Ordinal) { ["Ticket"] = model };

        if (reply is not null)
        {
            models["Reply"] = reply;
        }

        var email = new TemplatedEmail { Kind = kind, To = to, Models = models, Reference = model.Number };
        var content = new TemplatedEmailContent { Subject = subject, Html = html };

        if (ShellScope.Current is null)
        {
            await _sender.SendAsync(email, _ => Task.FromResult(content), cancellationToken);

            return;
        }

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<ITemplatedEmailSender>().SendAsync(email, _ => Task.FromResult(content)));
    }

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
/// <c>{{ Ticket.Status }}</c> (the status's label for customers), <c>{{ Ticket.RequesterName }}</c>.
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
    /// Builds the model of a ticket.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="status">Its status, if known.</param>
    /// <returns>The model.</returns>
    public static TicketEmailModel From(ContentItem ticket, TicketStatus status)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        ticket.TryGet<TicketPart>(out var part);

        return new TicketEmailModel
        {
            Number = part?.Number,
            Subject = ticket.DisplayText,
            Status = status is null ? null : string.IsNullOrWhiteSpace(status.CustomerLabel) ? status.Name : status.CustomerLabel,
            RequesterName = part?.RequesterContact?.Name,
        };
    }
}

/// <summary>
/// An agent's reply as an e-mail template reads it: <c>{{ Reply.Body }}</c> and <c>{{ Reply.AuthorName }}</c>.
/// </summary>
public sealed class TicketEmailReply
{
    /// <summary>
    /// Gets the reply's text.
    /// </summary>
    public string Body { get; init; }

    /// <summary>
    /// Gets the name of the agent who wrote it, if known.
    /// </summary>
    public string AuthorName { get; init; }
}
