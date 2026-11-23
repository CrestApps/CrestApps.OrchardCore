using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.PublicForms;
using CrestApps.OrchardCore.Tickets.Categories;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Portal;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// Opens a ticket from a Ticket Form widget, for a visitor with or without a login, and sends them back to the page with
/// the outcome in the query string, which the widget reads.
/// </summary>
[Feature(TicketsConstants.Features.WebForm)]
public sealed class TicketFormController : Controller
{
    internal const string SubmitRouteName = "TicketFormSubmit";
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
    private readonly IEnumerable<IPublicFormChallenge> _challenges;
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ITicketCategoryService _categories;

    public TicketFormController(IContentManager contentManager, ITicketService tickets, ICatalog<TicketDesk> desks, IEnumerable<IPublicFormChallenge> challenges, IAuthorizationService authorizationService, IContentDefinitionManager contentDefinitionManager, IContentItemDisplayManager contentItemDisplayManager, IUpdateModelAccessor updateModelAccessor, ITicketCategoryService categories)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _categories = categories;
        _authorizationService = authorizationService;
        _contentManager = contentManager;
        _tickets = tickets;
        _desks = desks;
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
    /// <param name="categoryId">The category, when the ticket type has categories.</param>
    /// <returns>A redirect back to the page.</returns>
    [AllowAnonymous]
    [HttpPost("tickets/forms/{widgetId}", Name = SubmitRouteName)]
    [RequestSizeLimit(MaxRequestBytes)]
    [global::OrchardCore.RateLimits.RateLimitGroup(TicketsConstants.RateLimitGroups.WebForm)]
    public async Task<IActionResult> Submit(string widgetId, string name, string email, string subject, string body, string returnUrl, string categoryId = null, string protectedLabel = null, string protectedValue = null)
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

        // A filled trap is answered as if it worked, so a robot learns nothing. How many requests a visitor may send is
        // Orchard Core's Rate Limits group of the form, across nodes (OnRateLimitedAsync answers the ones it refuses).
        if (PublicFormTrap.IsTrapped(Request.Form[PublicFormTrap.FieldName]))
        {
            return Back(back, widgetId, SentQueryName, "-");
        }

        // Every challenge that is set up (ReCaptcha, when that feature is on) must pass.
        foreach (var challenge in _challenges)
        {
            if (challenge.IsConfigured && !await challenge.VerifyAsync())
            {
                return Back(back, widgetId, ErrorQueryName, "challenge");
            }
        }

        var signedIn = User.Identity?.IsAuthenticated == true ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;
        var ticket = await _contentManager.NewAsync(part.TicketType);
        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(part.TicketType);
        var settings = type?.Parts.FirstOrDefault(item => item.PartDefinition.Name == TicketsConstants.PartName)?.GetSettings<TicketPartSettings>() ?? new TicketPartSettings();

        // Only a category customers can choose; it goes on the item before the form is read, for the type's field rules.
        var category = await TicketCategoryForm.ApplyAsync(_categories, ticket, categoryId, customersOnly: true);
        var categoryMissing = category is null
            && (!string.IsNullOrWhiteSpace(categoryId) || (settings.CategoryRequired && (await _categories.ListAsync(part.TicketType, customersOnly: true)).Count > 0));

        // The type's own form as customers fill it in; whatever was posted for anything else is dropped.
        TicketCustomerEditor.Begin(HttpContext);
        await _contentItemDisplayManager.UpdateEditorAsync(ticket, _updateModelAccessor.ModelUpdater, isNew: true);
        TicketCustomerEditor.KeepOnlyCustomerValues(ticket, await _contentManager.NewAsync(part.TicketType), type);

        subject = string.IsNullOrWhiteSpace(subject) ? ticket.DisplayText : subject;

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body) || categoryMissing || !ModelState.IsValid
            || (signedIn is null && (!LooksLikeEmail(email) || string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)))
        {
            return Back(back, widgetId, ErrorQueryName, "missing");
        }

        var result = await _tickets.CreateAsync(new TicketCreateRequest
        {
            Ticket = ticket,
            ContentType = part.TicketType,
            Subject = Cut(subject, MaxSubjectLength),
            Body = Cut(body, MaxBodyLength),
            DeskId = desk.ItemId,

            // The widget's desk gives way to the category's when that serves the same requesters.
            DeskIsSuggestion = true,
            CategoryId = category?.Id,
            Requester = signedIn is null ? default : new PartyRef(PartyKinds.User, signedIn),
            RequesterContact = signedIn is null ? new ContactInfo { Name = name.Trim(), Email = email.Trim() } : null,
            Channel = TicketsConstants.Channels.WebForm,

            // A guest's address is only what they typed.
            RequesterUnverified = signedIn is null,
        }, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return Back(back, widgetId, ErrorQueryName, "failed");
        }

        // A login or other secret the visitor shared, kept encrypted, apart from the ticket.
        if (!string.IsNullOrWhiteSpace(protectedValue) && HttpContext.RequestServices.GetService<ITicketProtectedInformation>() is { } protectedInformation)
        {
            var requesterPart = result.Ticket.GetOrCreate<TicketPart>();

            await protectedInformation.AddAsync(
                result.Ticket.ContentItemId,
                string.IsNullOrWhiteSpace(protectedLabel) ? HttpContext.RequestServices.GetRequiredService<IStringLocalizer<TicketFormController>>()["Shared by the requester"].Value : protectedLabel,
                protectedValue,
                new TicketActor { Party = requesterPart.Requester, Name = requesterPart.RequesterContact?.Name },
                fromRequester: true,
                HttpContext.RequestAborted);
        }

        return Back(back, widgetId, SentQueryName, result.Ticket.GetOrCreate<TicketPart>().Number);
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
            "missing" => S["Please give your name, a subject, your request, your e-mail address and every answer the form asks for."],
            "failed" => S["Your request couldn't be sent. Please try again later."],
            _ => null,
        };

    /// <summary>
    /// Answers a post the form's Rate Limits group refused: the visitor goes back to the page, which tells them to wait,
    /// as it did before the limit moved to Rate Limits.
    /// </summary>
    /// <param name="context">The refused request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the response is written.</returns>
    internal static ValueTask OnRateLimitedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        if (httpContext.GetRouteValue("widgetId")?.ToString() is not { Length: > 0 } widgetId)
        {
            return ValueTask.CompletedTask;
        }

        var url = httpContext.RequestServices.GetRequiredService<IUrlHelperFactory>()
            .GetUrlHelper(new ActionContext(httpContext, httpContext.GetRouteData(), new ActionDescriptor()));
        var returnUrl = httpContext.Request.Query["returnUrl"].ToString();

        httpContext.Response.StatusCode = StatusCodes.Status302Found;
        httpContext.Response.Headers.Location = Location(url, url.IsLocalUrl(returnUrl) ? returnUrl : "~/", widgetId, ErrorQueryName, "busy");

        return ValueTask.CompletedTask;
    }

    private RedirectResult Back(string url, string widgetId, string key, string value)
        => Redirect(Location(Url, url, widgetId, key, value));

    private static string Location(IUrlHelper urlHelper, string url, string widgetId, string key, string value)
    {
        var target = urlHelper.Content(url);
        var separator = target.Contains('?') ? '&' : '?';

        return $"{target}{separator}{FormQueryName}={Uri.EscapeDataString(widgetId)}&{key}={Uri.EscapeDataString(value ?? string.Empty)}#ticket-form-{widgetId}";
    }

    private static bool LooksLikeEmail(string email)
        => !string.IsNullOrWhiteSpace(email) && email.Trim().Length <= 254 && System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _);

    private static string Cut(string value, int length)
    {
        value = value?.Trim();

        return value?.Length > length ? value[..length] : value;
    }
}
