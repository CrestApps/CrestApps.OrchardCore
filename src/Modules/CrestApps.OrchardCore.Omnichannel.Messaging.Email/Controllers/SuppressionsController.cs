using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Controllers;

/// <summary>
/// The email suppression list: the addresses the business no longer sends to because they bounced, kept bouncing or
/// reported its mail as spam, with a way to take a wrongly suppressed address off and to add one by hand.
/// </summary>
[Admin("omnichannel/email/suppressions/{action}", "EmailSuppressions{action}")]
public sealed class SuppressionsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IEmailSuppressionList _suppressions;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SuppressionsController"/> class.
    /// </summary>
    /// <param name="suppressions">The suppression list.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SuppressionsController(
        IEmailSuppressionList suppressions,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<SuppressionsController> htmlLocalizer,
        IStringLocalizer<SuppressionsController> stringLocalizer)
    {
        _suppressions = suppressions;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the suppressed addresses.
    /// </summary>
    /// <param name="options">The search, and the bulk actions the list offers.</param>
    /// <param name="pagerParameters">The page to show and its size.</param>
    /// <param name="pagerOptions">The site's pager settings.</param>
    /// <param name="shapeFactory">The shape factory, for the pager.</param>
    public async Task<IActionResult> Index(
        CatalogEntryOptions<EmailSuppressionBulkAction> options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbid();
        }

        options ??= new CatalogEntryOptions<EmailSuppressionBulkAction>();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (items, total) = await _suppressions.PageAsync(options.Search, pager.Page, pager.PageSize, HttpContext.RequestAborted);

        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        options.BulkActions =
        [
            new SelectListItem(S["Remove From the List"], nameof(EmailSuppressionBulkAction.Remove)),
        ];

        // The admin-list tag builds a row per address, in the layout (List or Grid) the site or the user picked.
        return View(new EmailSuppressionsViewModel
        {
            Options = options,
            TotalCount = total,
            List = new AdminListContext<EmailSuppression>
            {
                Source = items,
                Pager = await shapeFactory.PagerAsync(pager, total, routeData),
            },
        });
    }

    /// <summary>
    /// Applies the search.
    /// </summary>
    /// <param name="model">The list, carrying the search.</param>
    /// <param name="pagerParameters">The page size to keep.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    public async Task<IActionResult> IndexFilterPost(EmailSuppressionsViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model?.Options?.Search },
            { "pageSize", pagerParameters.PageSize },
        });
    }

    /// <summary>
    /// Applies a bulk action to the selected addresses.
    /// </summary>
    /// <param name="options">The list options, carrying the bulk action and the search to return to.</param>
    /// <param name="itemIds">The selected addresses.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    public async Task<IActionResult> IndexPost(CatalogEntryOptions<EmailSuppressionBulkAction> options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbid();
        }

        var addresses = itemIds?
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (addresses.Length > 0)
        {
            switch (options?.BulkAction ?? EmailSuppressionBulkAction.None)
            {
                case EmailSuppressionBulkAction.None:
                    break;

                case EmailSuppressionBulkAction.Remove:
                    var counter = 0;

                    foreach (var address in addresses)
                    {
                        if (await _suppressions.RemoveAsync(address, HttpContext.RequestAborted))
                        {
                            counter++;
                        }
                    }

                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["None of the selected addresses was suppressed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter,
                            "1 address is no longer suppressed. A contact marked Do not email still is not emailed.",
                            "{0} addresses are no longer suppressed. A contact marked Do not email still is not emailed."));
                    }

                    break;

                default:
                    return BadRequest();
            }
        }

        return RedirectToIndex(options?.Search);
    }

    /// <summary>
    /// Suppresses an address by hand.
    /// </summary>
    /// <param name="address">The address to suppress.</param>
    /// <param name="note">Why it is suppressed.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string address, string note)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbid();
        }

        if (!OmnichannelEmailAddress.IsValid(OmnichannelEmailAddress.Normalize(address)))
        {
            await _notifier.ErrorAsync(H["Enter a valid email address."]);
        }
        else if (await _suppressions.SuppressAsync(address, EmailSuppressionReason.Manual, note, User.Identity?.Name, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["{0} is suppressed: nothing is sent to it from any address.", OmnichannelEmailAddress.Normalize(address)]);
        }
        else
        {
            await _notifier.InformationAsync(H["{0} was already suppressed.", OmnichannelEmailAddress.Normalize(address)]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Takes an address off the list, so it can be emailed again.
    /// </summary>
    /// <param name="address">The suppressed address.</param>
    /// <param name="returnUrl">The page to go back to, such as the list's current page and search.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string address, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbid();
        }

        if (await _suppressions.RemoveAsync(address, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["{0} is no longer suppressed. A contact marked Do not email still is not emailed.", address]);
        }
        else
        {
            await _notifier.WarningAsync(H["{0} was not suppressed.", address]);
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return this.LocalRedirect(returnUrl, true);
        }

        return RedirectToAction(nameof(Index));
    }

    private RedirectToActionResult RedirectToIndex(string search)
        => string.IsNullOrWhiteSpace(search)
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Index), new RouteValueDictionary
            {
                { _optionsSearch, search },
            });
}
