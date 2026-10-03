using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
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

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Lists the phone numbers known not to be in service, and lets a manager mark one by hand or remove a mark so the
/// number may be dialed again.
/// </summary>
/// <remarks>
/// A number that is not in service today can be given to somebody new, and a mark can be wrong, so every mark can be
/// removed here. Removing a mark dials nothing: it only lets later campaign loads and dialing use the number again.
/// </remarks>
[Admin]
public sealed class NotInServiceNumbersController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly INotInServiceNumberService _notInServiceNumbers;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotInServiceNumbersController"/> class.
    /// </summary>
    /// <param name="notInServiceNumbers">The list of numbers known not to be in service.</param>
    /// <param name="campaignCatalog">The campaigns, for the name of the campaign that found each number.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NotInServiceNumbersController(
        INotInServiceNumberService notInServiceNumbers,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<NotInServiceNumbersController> htmlLocalizer,
        IStringLocalizer<NotInServiceNumbersController> stringLocalizer)
    {
        _notInServiceNumbers = notInServiceNumbers;
        _campaignCatalog = campaignCatalog;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the numbers known not to be in service.
    /// </summary>
    [Admin("omnichannel/numbers-not-in-service", "OmnichannelNotInServiceNumbersIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions<NotInServiceNumberBulkAction> options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        options ??= new CatalogEntryOptions<NotInServiceNumberBulkAction>();

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());
        var result = await _notInServiceNumbers.PageAsync(pager.Page, pager.PageSize, options.Search, HttpContext.RequestAborted);

        var campaignIds = result.Entries
            .Select(entry => entry.CampaignId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var campaignNames = campaignIds.Length == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _campaignCatalog.GetAsync(campaignIds)).ToDictionary(campaign => campaign.ItemId, campaign => campaign.DisplayText, StringComparer.Ordinal);

        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        options.BulkActions =
        [
            new SelectListItem(S["Allow dialing"], nameof(NotInServiceNumberBulkAction.AllowDialing)),
        ];

        var model = new NotInServiceNumbersIndexViewModel
        {
            Options = options,
            TotalCount = result.Count,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var number in result.Entries)
        {
            model.Entries.Add(new NotInServiceNumberEntry
            {
                Number = number,
                CampaignName = number.CampaignId is not null && campaignNames.TryGetValue(number.CampaignId, out var name) ? name : null,
            });
        }

        return View(model);
    }

    /// <summary>
    /// Applies the search.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/numbers-not-in-service", "OmnichannelNotInServiceNumbersIndex")]
    public async Task<IActionResult> IndexFilterPost(NotInServiceNumbersIndexViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model?.Options?.Search },
        });
    }

    /// <summary>
    /// Applies a bulk action to the selected numbers.
    /// </summary>
    /// <param name="options">The list options, carrying the bulk action and the search to return to.</param>
    /// <param name="itemIds">The selected phone numbers.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("omnichannel/numbers-not-in-service", "OmnichannelNotInServiceNumbersIndex")]
    public async Task<IActionResult> IndexPost(CatalogEntryOptions<NotInServiceNumberBulkAction> options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        var phoneNumbers = itemIds?
            .Where(phoneNumber => !string.IsNullOrWhiteSpace(phoneNumber))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (phoneNumbers.Length > 0)
        {
            switch (options?.BulkAction ?? NotInServiceNumberBulkAction.None)
            {
                case NotInServiceNumberBulkAction.None:
                    break;

                case NotInServiceNumberBulkAction.AllowDialing:
                    var counter = 0;

                    foreach (var phoneNumber in phoneNumbers)
                    {
                        if (await _notInServiceNumbers.ClearAsync(phoneNumber, HttpContext.RequestAborted))
                        {
                            counter++;
                        }
                    }

                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["None of the selected numbers was marked as not in service."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter,
                            "1 number is no longer marked as not in service. Campaigns can load and dial it again.",
                            "{0} numbers are no longer marked as not in service. Campaigns can load and dial them again."));
                    }

                    break;

                default:
                    return BadRequest();
            }
        }

        return RedirectToIndex(options?.Search);
    }

    /// <summary>
    /// Marks a number as not in service by hand.
    /// </summary>
    [HttpPost]
    [Admin("omnichannel/numbers-not-in-service/mark", "OmnichannelNotInServiceNumbersMark")]
    public async Task<IActionResult> Mark(string phoneNumber)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        var marked = await _notInServiceNumbers.MarkAsync(new NotInServiceMark
        {
            PhoneNumber = phoneNumber,
            Source = OmnichannelConstants.NotInServiceSources.Manual,
            MarkedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
            MarkedByUsername = User.Identity?.Name,
        }, HttpContext.RequestAborted);

        if (marked is null)
        {
            await _notifier.ErrorAsync(H["\"{0}\" is not a phone number that can be read. Enter it with its country code, for example +17025550123.", phoneNumber]);
        }
        else
        {
            await _notifier.SuccessAsync(H["{0} is marked as not in service and will not be dialed.", marked.PhoneNumber]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Removes the not-in-service mark from a number, so campaigns can load and dial it again.
    /// </summary>
    /// <remarks>
    /// Nothing is dialed here. Activities already cancelled because of the mark stay cancelled.
    /// </remarks>
    [HttpPost]
    [Admin("omnichannel/numbers-not-in-service/allow-dialing", "OmnichannelNotInServiceNumbersAllowDialing")]
    public async Task<IActionResult> AllowDialing(string phoneNumber, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        if (await _notInServiceNumbers.ClearAsync(phoneNumber, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["{0} is no longer marked as not in service. Campaigns can load and dial it again.", phoneNumber]);
        }
        else
        {
            await _notifier.WarningAsync(H["{0} was not marked as not in service.", phoneNumber]);
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
