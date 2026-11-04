using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Marketing.ViewModels;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Marketing.Controllers;

/// <summary>
/// The unsubscribe link of a campaign email: the customer stops getting marketing, with one click to confirm.
/// </summary>
public sealed class UnsubscribeController : Controller
{
    /// <summary>
    /// Where an unsubscribe is said to come from, kept with the change.
    /// </summary>
    public const string ConsentSource = "MarketingUnsubscribe";

    private readonly CampaignService _campaigns;
    private readonly ICustomerStore _customers;
    private readonly IShapeFactory _shapeFactory;
    private readonly IConsentService _consentService;

    public UnsubscribeController(CampaignService campaigns, ICustomerStore customers, IShapeFactory shapeFactory, IConsentService consentService)
    {
        _campaigns = campaigns;
        _customers = customers;
        _shapeFactory = shapeFactory;
        _consentService = consentService;
    }

    [HttpGet("marketing/unsubscribe", Name = MarketingConstants.UnsubscribeRouteName)]
    public async Task<IActionResult> Index(string token)
    {
        var customer = await FindAsync(token);

        return View(await _shapeFactory.CreateAsync<UnsubscribeViewModel>("MarketingUnsubscribe", model =>
        {
            model.Token = token;
            model.IsValid = customer is not null;
            model.Email = customer?.Email;
            model.IsUnsubscribed = customer is { AcceptsMarketing: false };
        }));
    }

    // Mail apps' one-click unsubscribe posts without a form token, so the link's own token is the proof.
    [HttpPost("marketing/unsubscribe")]
    [IgnoreAntiforgeryToken]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost(string token)
    {
        var customer = await FindAsync(token);

        if (customer is { AcceptsMarketing: true })
        {
            // Recorded through the consent service, which every campaign and send asks.
            await _consentService.RecordAsync(ConsentChannels.Email, customer.Email, new ConsentChange
            {
                OptedOut = true,
                Source = ConsentSource,
                ContactKind = ConsentContactKinds.Customer,
                ContactId = customer.ItemId,
            }, HttpContext.RequestAborted);
        }

        return View(await _shapeFactory.CreateAsync<UnsubscribeViewModel>("MarketingUnsubscribe", model =>
        {
            model.Token = token;
            model.IsValid = customer is not null;
            model.Email = customer?.Email;
            model.IsUnsubscribed = customer is not null;
        }));
    }

    private async Task<Customers.Core.Models.Customer> FindAsync(string token)
        => _campaigns.ReadUnsubscribeToken(token) is { } customerId ? await _customers.FindByIdAsync(customerId, HttpContext.RequestAborted) : null;
}
