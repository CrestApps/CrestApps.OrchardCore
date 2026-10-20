using System.Security.Claims;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Fundraising.Services;
using CrestApps.OrchardCore.Fundraising.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Fundraising.Controllers;

/// <summary>
/// The donor's donations on the storefront account, and each one's receipt to print.
/// </summary>
[Authorize]
public sealed class AccountDonationsController : Controller
{
    private readonly IDonationStore _donations;
    private readonly ISiteService _siteService;

    public AccountDonationsController(IDonationStore donations, ISiteService siteService)
    {
        _donations = donations;
        _siteService = siteService;
    }

    [HttpGet("account/donations", Name = "AccountDonations")]
    public async Task<IActionResult> Index()
        => View(new AccountDonationsViewModel
        {
            Donations = [.. await _donations.ListByDonorAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted)],
        });

    [HttpGet("account/donations/{id}/receipt", Name = "AccountDonationReceipt")]
    public async Task<IActionResult> Receipt(string id)
    {
        var donation = await _donations.FindByIdAsync(id, HttpContext.RequestAborted);

        // A donor sees only their own receipts.
        if (donation is null || string.IsNullOrEmpty(donation.DonorUserId) || donation.DonorUserId != User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return NotFound();
        }

        var site = await _siteService.GetSiteSettingsAsync();

        return View(new DonationReceiptViewModel
        {
            Donation = donation,
            Settings = site.GetOrCreate<FundraisingSettings>(),
            SiteName = site.SiteName,
        });
    }
}
