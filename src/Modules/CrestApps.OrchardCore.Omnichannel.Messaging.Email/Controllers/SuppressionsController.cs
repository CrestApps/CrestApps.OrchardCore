using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Controllers;

/// <summary>
/// The email suppression list: the addresses the business no longer sends to because they bounced, kept bouncing or
/// reported its mail as spam, with a way to take a wrongly suppressed address off and to add one by hand.
/// </summary>
[Admin("omnichannel/email/suppressions/{action}", "EmailSuppressions{action}")]
public sealed class SuppressionsController : Controller
{
    private const int PageSize = 50;

    private readonly IEmailSuppressionList _suppressions;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public SuppressionsController(
        IEmailSuppressionList suppressions,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<SuppressionsController> htmlLocalizer)
    {
        _suppressions = suppressions;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    public async Task<IActionResult> Index(string search, int page = 1)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbid();
        }

        var (items, total) = await _suppressions.PageAsync(search, page, PageSize, HttpContext.RequestAborted);

        return View(new EmailSuppressionsViewModel
        {
            Search = search,
            Page = Math.Max(1, page),
            PageSize = PageSize,
            Total = total,
            Items = items,
        });
    }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string address, string search, int page = 1)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbid();
        }

        if (await _suppressions.RemoveAsync(address, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["{0} is no longer suppressed. A contact marked Do not email still is not emailed.", address]);
        }

        return RedirectToAction(nameof(Index), new { search, page });
    }
}
