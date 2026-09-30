using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Lists the phone numbers known not to be in service, and lets a manager clear a mark or add one by hand.
/// </summary>
/// <remarks>
/// A number that is not in service today can be given to somebody new, and a mark can be wrong, so every mark can be
/// cleared here and the number is dialed again from then on.
/// </remarks>
[Admin]
public sealed class NotInServiceNumbersController : Controller
{
    private readonly INotInServiceNumberService _notInServiceNumbers;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotInServiceNumbersController"/> class.
    /// </summary>
    /// <param name="notInServiceNumbers">The list of numbers known not to be in service.</param>
    /// <param name="campaignCatalog">The campaigns, for the name of the campaign that found each number.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public NotInServiceNumbersController(
        INotInServiceNumberService notInServiceNumbers,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<NotInServiceNumbersController> htmlLocalizer)
    {
        _notInServiceNumbers = notInServiceNumbers;
        _campaignCatalog = campaignCatalog;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the numbers known not to be in service.
    /// </summary>
    [Admin("omnichannel/numbers-not-in-service", "OmnichannelNotInServiceNumbersIndex")]
    public async Task<IActionResult> Index(
        string search,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());
        var result = await _notInServiceNumbers.PageAsync(pager.Page, pager.PageSize, search, HttpContext.RequestAborted);

        var campaignIds = result.Entries
            .Select(entry => entry.CampaignId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var campaignNames = campaignIds.Length == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _campaignCatalog.GetAsync(campaignIds)).ToDictionary(campaign => campaign.ItemId, campaign => campaign.DisplayText, StringComparer.Ordinal);

        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(search))
        {
            routeData.Values.TryAdd(nameof(search), search);
        }

        var model = new NotInServiceNumbersIndexViewModel
        {
            Search = search,
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
    public async Task<IActionResult> IndexFilterPost(string search)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { nameof(search), search },
        });
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
    /// Clears the mark from a number, so it is dialed again.
    /// </summary>
    [HttpPost]
    [Admin("omnichannel/numbers-not-in-service/clear", "OmnichannelNotInServiceNumbersClear")]
    public async Task<IActionResult> Clear(string phoneNumber, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        if (await _notInServiceNumbers.ClearAsync(phoneNumber, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["{0} is no longer marked as not in service and can be dialed again.", phoneNumber]);
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
}
