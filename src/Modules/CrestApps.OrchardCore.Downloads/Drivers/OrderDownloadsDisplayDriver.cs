using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Downloads.ViewModels;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Downloads.Drivers;

/// <summary>
/// The downloads section of an order's pages: links for the buyer, and counts and controls for the store.
/// </summary>
internal sealed class OrderDownloadsDisplayDriver : DisplayDriver<Order>
{
    private readonly DownloadService _downloads;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    public OrderDownloadsDisplayDriver(
        DownloadService downloads,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IClock clock)
    {
        _downloads = downloads;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _clock = clock;
    }

    public override async Task<IDisplayResult> DisplayAsync(Order order, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrderConstants.DisplayTypes.DetailAdmin or OrderConstants.DisplayTypes.Detail))
        {
            return null;
        }

        var entitlements = await _downloads.ListAsync(order.ItemId);

        if (entitlements.Count == 0)
        {
            return null;
        }

        var canManage = context.DisplayType == OrderConstants.DisplayTypes.DetailAdmin &&
            await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, DownloadsPermissions.ManageDownloads);

        // The buyer sees only what they can still download; the store sees everything, with why a link stopped.
        if (!canManage && order.Status != OrderStatus.Paid)
        {
            return null;
        }

        var now = _clock.UtcNow;

        return Initialize<OrderDownloadsViewModel>(canManage ? "OrderDownloads_DetailAdmin" : "OrderDownloads_Detail", async model =>
        {
            model.Order = order;
            model.CanManage = canManage;

            foreach (var entitlement in entitlements)
            {
                var problem = DownloadService.GetProblem(entitlement, now);

                model.Downloads.Add(new OrderDownloadViewModel
                {
                    Entitlement = entitlement,
                    Problem = problem,
                    Url = canManage || problem == Models.DownloadProblem.None ? await _downloads.CreateUrlAsync(entitlement) : null,
                });
            }
        }).Location("Content:4");
    }
}
