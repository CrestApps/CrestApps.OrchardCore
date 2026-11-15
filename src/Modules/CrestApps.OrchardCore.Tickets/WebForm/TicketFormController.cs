using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// Opens a ticket from a Ticket Form widget, for a visitor with or without a login, and sends them back to the page with
/// the outcome in the query string, which the widget reads.
/// </summary>
[Feature(TicketsConstants.Features.WebForm)]
public sealed class TicketFormController : Controller
{
    internal const string FormQueryName = "ticketForm";
    internal const string SentQueryName = "ticketSent";
    internal const string ErrorQueryName = "ticketError";

    private const int MaxSubjectLength = 200;
    private const int MaxBodyLength = 10_000;
    private const int MaxNameLength = 200;

    // A form post carries no files: subject, body and a few short fields.
    private const long MaxRequestBytes = 64 * 1024;

    private readonly IContentManager _contentManager;
    private readonly ITicketService _tickets;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly TicketFormGuard _guard;
    private readonly IEnumerable<ITicketFormChallenge> _challenges;
    private readonly IAuthorizationService _authorizationService;

    public TicketFormController(IContentManager contentManager, ITicketService tickets, ICatalog<TicketDesk> desks, TicketFormGuard guard, IEnumerable<ITicketFormChallenge> challenges, IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
        _contentManager = contentManager;
        _tickets = tickets;
        _desks = desks;
        _guard = guard;
        _challenges = challenges;
    }

    /// <summary>
    /// Opens a ticket from a widget's form.
    /// </summary>
    /// <param name="widgetId">The widget.</param>
    /// <param name="name">The visitor's name.</param>
    /// <param name="email">Their e-mail.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="body">The request.</param>
    /// <param name="returnUrl">The page to go back to.</param>
    /// <returns>A redirect back to the page.</returns>
    [AllowAnonymous]
    [HttpPost("tickets/forms/{widgetId}", Name = "TicketFormSubmit")]
    [RequestSizeLimit(MaxRequestBytes)]
    [global::OrchardCore.RateLimits.RateLimitGroup(TicketsConstants.RateLimitGroups.WebForm)]
    public async Task<IActionResult> Submit(string widgetId, string name, string email, string subject, string body, string returnUrl)
    {
        var back = Url.IsLocalUrl(returnUrl) ? returnUrl : "~/";

        if (await _contentManager.GetAsync(widgetId, VersionOptions.Published) is not { } widget
            || !widget.TryGet<TicketFormPart>(out var part)
            || string.IsNullOrEmpty(part.TicketType)
            || await _desks.FindByIdAsync(part.DeskId) is not { } desk
            || !await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, widget))
        {
            // A form the visitor can't see, or one no longer published, takes no requests.
            return NotFound();
        }

        // A filled trap is answered as if it worked, so a robot learns nothing.
        if (TicketFormGuard.IsTrapped(Request.Form[TicketFormGuard.TrapFieldName]))
        {
            return Back(back, widgetId, SentQueryName, "-");
        }

        // A last line on this node, per visitor address; Orchard's Rate Limits feature, when on, holds the form across
        // nodes by the visitor (their user, or their address as Reverse Proxy gives it).
        if (!_guard.TryTake(HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            return Back(back, widgetId, ErrorQueryName, "busy");
        }

        if (_challenges.FirstOrDefault() is { IsConfigured: true } challenge && !await challenge.VerifyAsync())
        {
            return Back(back, widgetId, ErrorQueryName, "challenge");
        }

        var signedIn = User.Identity?.IsAuthenticated == true ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body)
            || (signedIn is null && (!LooksLikeEmail(email) || string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)))
        {
            return Back(back, widgetId, ErrorQueryName, "missing");
        }

        var ticket = await _contentManager.NewAsync(part.TicketType);
        var result = await _tickets.CreateAsync(new TicketCreateRequest
        {
            Ticket = ticket,
            ContentType = part.TicketType,
            Subject = Cut(subject, MaxSubjectLength),
            Body = Cut(body, MaxBodyLength),
            DeskId = desk.ItemId,
            Requester = signedIn is null ? default : new PartyRef(PartyKinds.User, signedIn),
            RequesterContact = signedIn is null ? new ContactInfo { Name = name.Trim(), Email = email.Trim() } : null,
            Channel = TicketsConstants.Channels.WebForm,

            // A guest's address is only what they typed.
            RequesterUnverified = signedIn is null,
        }, HttpContext.RequestAborted);

        return result.Succeeded
            ? Back(back, widgetId, SentQueryName, result.Ticket.GetOrCreate<TicketPart>().Number)
            : Back(back, widgetId, ErrorQueryName, "failed");
    }

    /// <summary>
    /// Describes an outcome code for the visitor.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="S">The localizer.</param>
    /// <returns>The message, or <see langword="null"/>.</returns>
    internal static string Describe(string code, IStringLocalizer S)
        => code switch
        {
            "busy" => S["You sent several requests in a short time. Please wait a few minutes and try again."],
            "challenge" => S["Please confirm you're not a robot and send it again."],
            "missing" => S["Please give your name, a subject, your request and your e-mail address."],
            "failed" => S["Your request couldn't be sent. Please try again later."],
            _ => null,
        };

    private RedirectResult Back(string url, string widgetId, string key, string value)
    {
        var target = Url.Content(url);
        var separator = target.Contains('?') ? '&' : '?';

        return Redirect($"{target}{separator}{FormQueryName}={Uri.EscapeDataString(widgetId)}&{key}={Uri.EscapeDataString(value ?? string.Empty)}#ticket-form-{widgetId}");
    }

    private static bool LooksLikeEmail(string email)
        => !string.IsNullOrWhiteSpace(email) && email.Trim().Length <= 254 && System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _);

    private static string Cut(string value, int length)
    {
        value = value?.Trim();

        return value?.Length > length ? value[..length] : value;
    }
}
