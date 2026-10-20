using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Marketing.ViewModels;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Marketing.Controllers;

/// <summary>
/// The unsubscribe link of a campaign email: the customer stops getting marketing, with one click to confirm.
/// </summary>
public sealed class UnsubscribeController : Controller
{
    private readonly CampaignService _campaigns;
    private readonly ICustomerStore _customers;
    private readonly IShapeFactory _shapeFactory;
    private readonly IClock _clock;

    public UnsubscribeController(CampaignService campaigns, ICustomerStore customers, IShapeFactory shapeFactory, IClock clock)
    {
        _campaigns = campaigns;
        _customers = customers;
        _shapeFactory = shapeFactory;
        _clock = clock;
    }

    [HttpGet("marketing/unsubscribe", Name = "MarketingUnsubscribe")]
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
            customer.AcceptsMarketing = false;
            customer.ModifiedUtc = _clock.UtcNow;
            await _customers.UpdateAsync(customer, HttpContext.RequestAborted);
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
