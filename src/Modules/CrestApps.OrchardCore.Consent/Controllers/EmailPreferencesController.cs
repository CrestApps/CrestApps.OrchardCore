using CrestApps.Core.Support;
using CrestApps.OrchardCore.Consent.Unsubscribe;
using CrestApps.OrchardCore.Consent.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Consent.Controllers;

/// <summary>
/// The site's one e-mail preferences page, which every unsubscribe link opens: it shows what the link stops (a list, a
/// campaign, or all marketing e-mail), with a button to stop it and a button to stop all marketing e-mail. Opening the
/// page changes nothing, so a mail scanner that follows the link unsubscribes nobody; only a button does. A mail app's
/// RFC 8058 one-click unsubscribe posts to the link itself.
/// </summary>
/// <remarks>
/// The endpoints take no permission: the signed token in the link is the authorization, as on Marketing's unsubscribe
/// page. The person following a link in an e-mail usually has no account, and the token names one address and one
/// scope, can't be altered, and expires.
/// </remarks>
[AllowAnonymous]
public sealed class EmailPreferencesController : Controller
{
    /// <summary>
    /// Where a change made through an unsubscribe link is said to come from, kept with it.
    /// </summary>
    public const string ConsentSource = "unsubscribe-link";

    internal const string RouteName = "ConsentEmailPreferences";
    internal const string UpdateRouteName = "ConsentEmailPreferencesUpdate";

    // The token travels in the query string (?token=). IIS (HTTP.sys) refuses a path segment longer than 260
    // characters, and a signed token can be longer than that. The path form is kept for links that carry it.
    private const string QueryRoute = ConsentConstants.PreferencesPath;
    private const string LinkRoute = ConsentConstants.PreferencesPath + "/{token}";
    private const string OneClickField = "List-Unsubscribe";
    private const string OneClickValue = "One-Click";

    private readonly IUnsubscribeLinks _links;
    private readonly IEnumerable<IUnsubscribeScopeHandler> _scopeHandlers;
    private readonly IConsentService _consentService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailPreferencesController"/> class.
    /// </summary>
    /// <param name="links">The unsubscribe links, which read the token.</param>
    /// <param name="scopeHandlers">The handlers of the scope kinds other modules add, such as mailing lists.</param>
    /// <param name="consentService">The consent service, which records "stop all marketing e-mail".</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmailPreferencesController(
        IUnsubscribeLinks links,
        IEnumerable<IUnsubscribeScopeHandler> scopeHandlers,
        IConsentService consentService,
        ILogger<EmailPreferencesController> logger,
        IStringLocalizer<EmailPreferencesController> stringLocalizer)
    {
        _links = links;
        _scopeHandlers = scopeHandlers;
        _consentService = consentService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the preferences page of an unsubscribe link. It changes nothing.
    /// </summary>
    /// <param name="token">The link's token.</param>
    [HttpGet(QueryRoute, Name = RouteName)]
    [HttpGet(LinkRoute)]
    public async Task<IActionResult> Index(string token)
    {
        KeepPrivate();

        if (!_links.TryRead(token, out var ticket))
        {
            return View(new EmailPreferencesViewModel { Token = token });
        }

        return View(await BuildAsync(token, ticket));
    }

    /// <summary>
    /// The RFC 8058 one-click unsubscribe: a mail app posts <c>List-Unsubscribe=One-Click</c> to the link, and the
    /// link's scope is stopped. A mail app posts without a form token, so the link's own token is the proof.
    /// </summary>
    /// <param name="token">The link's token.</param>
    [HttpPost(QueryRoute)]
    [HttpPost(LinkRoute)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> OneClick(string token)
    {
        KeepPrivate();

        if (!Request.HasFormContentType)
        {
            return BadRequest();
        }

        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);

        if (!string.Equals(form[OneClickField], OneClickValue, StringComparison.OrdinalIgnoreCase) ||
            !_links.TryRead(token, out var ticket))
        {
            return BadRequest();
        }

        var handler = FindHandler(ticket.Scope);

        if (handler is null)
        {
            // All marketing, or a kind no enabled feature handles any more: the request is honored all the same, as
            // the broadest stop it can be.
            await StopAllMarketingAsync(ticket);
        }
        else
        {
            await handler.UnsubscribeAsync(ticket, HttpContext.RequestAborted);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("A one-click unsubscribe stopped the scope '{ScopeKind}'.", ticket.Scope.Kind.SanitizeLogValue());
        }

        return Ok();
    }

    /// <summary>
    /// Applies the button the person pressed on the preferences page: stop the link's scope, or stop all marketing
    /// e-mail.
    /// </summary>
    /// <param name="token">The link's token.</param>
    /// <param name="stopAll">Whether the person pressed "Stop all marketing e-mail".</param>
    [HttpPost(QueryRoute + "/update", Name = UpdateRouteName)]
    [HttpPost(LinkRoute + "/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string token, bool stopAll)
    {
        KeepPrivate();

        if (!_links.TryRead(token, out var ticket))
        {
            return View(nameof(Index), new EmailPreferencesViewModel { Token = token });
        }

        var handler = FindHandler(ticket.Scope);

        if (handler is not null)
        {
            await handler.UnsubscribeAsync(ticket, HttpContext.RequestAborted);
        }

        var stoppedAll = stopAll || handler is null;

        if (stoppedAll)
        {
            await StopAllMarketingAsync(ticket);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The e-mail preferences page stopped the scope '{ScopeKind}', all marketing: {StoppedAllMarketing}.",
                ticket.Scope.Kind.SanitizeLogValue(),
                stoppedAll);
        }

        var model = await BuildAsync(token, ticket);
        model.Unsubscribed = true;
        model.StoppedAllMarketing = stoppedAll;

        return View(nameof(Index), model);
    }

    private async Task<EmailPreferencesViewModel> BuildAsync(string token, UnsubscribeTicket ticket)
    {
        var model = new EmailPreferencesViewModel
        {
            Token = token,
            IsValid = true,
            Email = ticket.Address,
        };

        var handler = FindHandler(ticket.Scope);

        if (handler is null)
        {
            model.IsAllMarketing = true;
            model.ScopeTitle = S["all marketing e-mail"];

            return model;
        }

        model.ScopeTitle = await handler.GetTitleAsync(ticket.Scope, HttpContext.RequestAborted);
        model.CanUnsubscribeScope = !string.IsNullOrEmpty(model.ScopeTitle);

        return model;
    }

    // All marketing has no handler of its own; neither has a kind whose feature was turned off.
    private IUnsubscribeScopeHandler FindHandler(UnsubscribeScope scope)
    {
        if (string.Equals(scope.Kind, UnsubscribeScopeKinds.AllMarketing, StringComparison.Ordinal))
        {
            return null;
        }

        return _scopeHandlers.FirstOrDefault(handler => string.Equals(handler.Kind, scope.Kind, StringComparison.Ordinal));
    }

    private Task StopAllMarketingAsync(UnsubscribeTicket ticket)
        => _consentService.RecordAsync(ConsentChannels.Email, ticket.Address, new ConsentChange
        {
            OptedOut = true,
            Purpose = ConsentPurposes.Marketing,
            Source = ConsentSource,
        }, HttpContext.RequestAborted);

    // The token in the path is a key to the person's preferences: the page is never indexed, cached or sent on as a
    // referrer.
    private void KeepPrivate()
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
