using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Orders.Services;

public sealed class OrderLinkService : IOrderLinkService
{
    /// <summary>
    /// How long a link opens its order, like a pay link.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    private const string ProtectionPurpose = "CrestApps.OrchardCore.Orders.OrderLink";

    private readonly SignedTokenService _signedTokenService;
    private readonly SiteUrlGenerator _siteUrlGenerator;

    public OrderLinkService(
        SignedTokenService signedTokenService,
        SiteUrlGenerator siteUrlGenerator)
    {
        _signedTokenService = signedTokenService;
        _siteUrlGenerator = siteUrlGenerator;
    }

    public Task<string> CreateUrlAsync(Order order)
        => CreateUrlAsync(order, OrderConstants.RouteNames.View);

    public Task<string> CreatePayUrlAsync(Order order)
        => CreateUrlAsync(order, OrderConstants.RouteNames.Pay);

    private Task<string> CreateUrlAsync(Order order, string routeName)
    {
        ArgumentNullException.ThrowIfNull(order);

        return _siteUrlGenerator.GetUrlAsync(routeName, new { token = _signedTokenService.Protect(ProtectionPurpose, order.ItemId, Lifetime) });
    }

    public string GetOrderId(string token)
        => _signedTokenService.Unprotect(ProtectionPurpose, token);
}
