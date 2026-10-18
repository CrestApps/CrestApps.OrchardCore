using System.Security.Cryptography;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Services;

public sealed class OrderLinkService : IOrderLinkService
{
    /// <summary>
    /// How long a link opens its order, like a pay link.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    private const string ProtectionPurpose = "CrestApps.OrchardCore.Orders.OrderLink";

    private readonly ITimeLimitedDataProtector _protector;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public OrderLinkService(
        IDataProtectionProvider dataProtectionProvider,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose).ToTimeLimitedDataProtector();
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
    }

    public async Task<string> CreateUrlAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var token = _protector.Protect(order.ItemId, Lifetime);
        var site = await _siteService.GetSiteSettingsAsync();

        // An email is often sent where there is no request, so the site's own address comes first.
        if (!string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return $"{site.BaseUrl.TrimEnd('/')}/orders/view/{Uri.EscapeDataString(token)}";
        }

        var httpContext = _httpContextAccessor.HttpContext;

        return httpContext is null
            ? null
            : _linkGenerator.GetUriByRouteValues(httpContext, OrderConstants.RouteNames.View, new { token });
    }

    public string GetOrderId(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(token, out _);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
