using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Marketing.ViewModels;
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
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketing.Controllers;

/// <summary>
/// Commerce › Campaigns: emails to a segment's customers who accept marketing, sent now or at a time.
/// </summary>
[Admin("marketing/campaigns/{action}/{itemId?}", "Campaigns{action}")]
public sealed class CampaignsAdminController : Controller
{
    public const string DefaultBody = """
        <p>Hi {{ Customer.FirstName }},</p>
        <p>It's been a while! Here's what's new at {{ StoreName }}.</p>
        <p><a href="{{ UnsubscribeUrl }}">Unsubscribe</a> from these emails.</p>
        """;

    private readonly ICampaignStore _campaigns;
    private readonly ISegmentStore _segments;
    private readonly CampaignService _campaignService;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CampaignsAdminController(
        ICampaignStore campaigns,
        ISegmentStore segments,
        CampaignService campaignService,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        ILocalClock localClock,
        IClock clock,
        IHtmlLocalizer<CampaignsAdminController> htmlLocalizer,
        IStringLocalizer<CampaignsAdminController> stringLocalizer)
    {
        _campaigns = campaigns;
        _segments = segments;
        _campaignService = campaignService;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _localClock = localClock;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("marketing/campaigns", "Campaigns")]
    public async Task<IActionResult> Index(string q, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, campaigns) = await _campaigns.PageAsync(q, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeData.Values.TryAdd("q", q);
        }

        return View(new CampaignsIndexViewModel
        {
            Search = q,
            List = new AdminListContext<Campaign>
            {
                Source = campaigns,
                Pager = (IShape)await shapeFactory.PagerAsync(pager, count, routeData),
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        return View("Edit", await PopulateAsync(new CampaignEditViewModel { Body = DefaultBody }, campaign: null));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(CampaignEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        var campaign = new Campaign { ItemId = IdGenerator.GenerateId(), CreatedUtc = _clock.UtcNow, CreatedBy = User.Identity?.Name };

        if (!await ApplyAsync(model, campaign))
        {
            return View("Edit", await PopulateAsync(model, campaign: null));
        }

        await _campaigns.CreateAsync(campaign);

        return await AfterSaveAsync(campaign, model, submit);
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        if (await _campaigns.FindByIdAsync(itemId) is not { } campaign)
        {
            return NotFound();
        }

        return View(await PopulateAsync(new CampaignEditViewModel
        {
            Name = campaign.Name,
            SegmentId = campaign.SegmentId,
            Subject = campaign.Subject,
            Body = campaign.Body,
            ScheduledLocal = campaign.ScheduledUtc is { } scheduled ? (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(scheduled, DateTimeKind.Utc))).DateTime : null,
        }, campaign));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, CampaignEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        if (await _campaigns.FindByIdAsync(itemId) is not { } campaign)
        {
            return NotFound();
        }

        // A campaign that's sending or sent only cancels.
        if (submit == "cancel")
        {
            if (campaign.Status is CampaignStatus.Scheduled or CampaignStatus.Sending)
            {
                campaign.Status = CampaignStatus.Canceled;
                campaign.ModifiedUtc = _clock.UtcNow;
                await _campaigns.UpdateAsync(campaign);
                await _notifier.InformationAsync(H["The campaign was canceled. {0} emails had gone out.", campaign.Sent]);
            }

            return RedirectToAction(nameof(Edit), new { itemId });
        }

        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled))
        {
            return RedirectToAction(nameof(Edit), new { itemId });
        }

        if (!await ApplyAsync(model, campaign))
        {
            return View(await PopulateAsync(model, campaign));
        }

        // Saving a scheduled campaign keeps it a draft until it's sent again.
        campaign.Status = CampaignStatus.Draft;
        campaign.ModifiedUtc = _clock.UtcNow;
        await _campaigns.UpdateAsync(campaign);

        return await AfterSaveAsync(campaign, model, submit);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        if (await _campaigns.FindByIdAsync(itemId) is not { } campaign)
        {
            return NotFound();
        }

        if (campaign.Status == CampaignStatus.Sending)
        {
            await _notifier.ErrorAsync(H["Cancel the campaign before deleting it."]);

            return RedirectToAction(nameof(Index));
        }

        await _campaigns.DeleteAsync(campaign);
        await _notifier.SuccessAsync(H["The campaign was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    // "test" sends it to the test address and stays; "send" sends it now or at its time; "save" keeps it a draft.
    private async Task<IActionResult> AfterSaveAsync(Campaign campaign, CampaignEditViewModel model, string submit)
    {
        if (submit == "test")
        {
            if (await _campaignService.SendTestAsync(campaign, model.TestEmail, UnsubscribeUrl, HttpContext.RequestAborted))
            {
                await _notifier.SuccessAsync(H["A test was emailed to {0}.", model.TestEmail]);
            }
            else
            {
                await _notifier.ErrorAsync(H["The test couldn't be emailed. Enter an address, and check the site's email settings."]);
            }
        }
        else if (submit == "send")
        {
            if (string.IsNullOrWhiteSpace((await _siteService.GetSiteSettingsAsync()).BaseUrl) || !_campaignService.CanSend)
            {
                await _notifier.ErrorAsync(H["Campaigns need the site's email set up and its base URL, for the unsubscribe link. It was saved as a draft."]);
            }
            else if (campaign.ScheduledUtc is { } scheduled && scheduled > _clock.UtcNow)
            {
                campaign.Status = CampaignStatus.Scheduled;
                await _campaigns.UpdateAsync(campaign);
                await _notifier.SuccessAsync(H["The campaign is scheduled. It starts sending at its time."]);
            }
            else
            {
                campaign.Status = CampaignStatus.Sending;
                campaign.StartedUtc = _clock.UtcNow;
                await _campaigns.UpdateAsync(campaign);
                await _notifier.SuccessAsync(H["The campaign is sending. It goes out in batches over the next minutes."]);
            }
        }
        else
        {
            await _notifier.SuccessAsync(H["The campaign was saved as a draft."]);
        }

        return RedirectToAction(nameof(Edit), new { itemId = campaign.ItemId });
    }

    private async Task<bool> ApplyAsync(CampaignEditViewModel model, Campaign campaign)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["Name the campaign. Only staff see it."]);
        }

        if (string.IsNullOrEmpty(model.SegmentId) || await _segments.FindByIdAsync(model.SegmentId) is null)
        {
            ModelState.AddModelError(nameof(model.SegmentId), S["Choose the segment it goes to."]);
        }

        if (string.IsNullOrWhiteSpace(model.Subject))
        {
            ModelState.AddModelError(nameof(model.Subject), S["Write the email's subject."]);
        }

        if (string.IsNullOrWhiteSpace(model.Body))
        {
            ModelState.AddModelError(nameof(model.Body), S["Write the email."]);
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        campaign.Name = model.Name.Trim();
        campaign.SegmentId = model.SegmentId;
        campaign.Subject = model.Subject.Trim();
        campaign.Body = model.Body;
        campaign.ScheduledUtc = model.ScheduledLocal is { } local ? (await _localClock.ConvertToUtcAsync(local)) : null;

        return true;
    }

    private async Task<CampaignEditViewModel> PopulateAsync(CampaignEditViewModel model, Campaign campaign)
    {
        model.ItemId = campaign?.ItemId;
        model.Campaign = campaign;
        model.CanEmail = _campaignService.CanSend;
        model.HasBaseUrl = !string.IsNullOrWhiteSpace((await _siteService.GetSiteSettingsAsync()).BaseUrl);
        model.Segments = [.. (await _segments.ListAllAsync(HttpContext.RequestAborted)).Select(segment => new SelectListItem(segment.Name, segment.ItemId, segment.ItemId == model.SegmentId))];

        return model;
    }

    private string UnsubscribeUrl(string token)
        => Url.RouteUrl(MarketingConstants.UnsubscribeRouteName, new { token }, Request.Scheme);
}
