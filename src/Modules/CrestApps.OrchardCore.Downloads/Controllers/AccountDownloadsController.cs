using System.Security.Claims;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Downloads.ViewModels;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Downloads.Controllers;

/// <summary>
/// Everything the signed-in customer can download, from all their paid orders, on the storefront account. The list is
/// the <c>AccountDownloads</c> shape, so a theme can override it.
/// </summary>
[Authorize]
public sealed class AccountDownloadsController : Controller
{
    // A customer's recent paid orders are enough for a page of downloads; older ones stay on each order's page.
    private const int MaxOrders = 200;

    private readonly IOrderStore _orderStore;
    private readonly DownloadService _downloads;
    private readonly IClock _clock;
    private readonly IShapeFactory _shapeFactory;

    public AccountDownloadsController(IOrderStore orderStore, DownloadService downloads, IClock clock, IShapeFactory shapeFactory)
    {
        _orderStore = orderStore;
        _downloads = downloads;
        _clock = clock;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("account/downloads", Name = "AccountDownloads")]
    public async Task<IActionResult> Index()
    {
        var (_, orders) = await _orderStore.PageAsync(new OrderQuery
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Statuses = [OrderStatus.Paid],
        }, 0, MaxOrders, HttpContext.RequestAborted);

        var now = _clock.UtcNow;
        var downloads = new List<AccountDownloadViewModel>();

        foreach (var order in orders)
        {
            foreach (var entitlement in await _downloads.ListAsync(order.ItemId, HttpContext.RequestAborted))
            {
                var problem = DownloadService.GetProblem(entitlement, now);

                downloads.Add(new AccountDownloadViewModel
                {
                    Order = order,
                    Entitlement = entitlement,
                    Problem = problem,
                    Url = problem == DownloadProblem.None ? await _downloads.CreateUrlAsync(entitlement) : null,
                });
            }
        }

        // What still works first, newest first.
        List<AccountDownloadViewModel> ordered = [.. downloads
            .OrderBy(download => download.Problem == DownloadProblem.None ? 0 : 1)
            .ThenByDescending(download => download.Entitlement.CreatedUtc)];

        return View(await _shapeFactory.CreateAsync<AccountDownloadsViewModel>("AccountDownloads", model =>
        {
            model.Downloads = ordered;
        }));
    }
}
