using CrestApps.OrchardCore.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The setup guide's "Protect payments": the Payment Rate Limits feature limits how often the payment and code routes can
/// be called. The card-testing protection works without it. A store behind a proxy must also trust the proxy, or every
/// buyer looks like the proxy and shares one limit.
/// </summary>
internal sealed class ProtectPaymentsSetupStep : IStoreSetupStep
{
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public ProtectPaymentsSetupStep(
        IShellFeaturesManager shellFeaturesManager,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ProtectPaymentsSetupStep> stringLocalizer)
    {
        _shellFeaturesManager = shellFeaturesManager;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public string Name
        => "protect-payments";

    public int Order
        => 37;

    public async Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var enabled = (await _shellFeaturesManager.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rateLimitsOn = enabled.Contains(CheckoutConstants.Features.RateLimits);
        var httpContext = _httpContextAccessor.HttpContext;

        // A request that came through a proxy carries the header, and the forwarded-headers middleware takes it off once it
        // applied it. Still on the request means no one applied it: the Reverse Proxy feature is off, or on without
        // forwarding X-Forwarded-For (it forwards nothing by default). Then every buyer has the proxy's address and every
        // per-network limit, the always-on card-testing ones included, is shared by the whole store.
        var behindUntrustedProxy = httpContext?.Request.Headers.ContainsKey("X-Forwarded-For") == true;

        return new StoreSetupStepDescription
        {
            Title = S["Protect payments"],
            Description = S["Limits how often a visitor can try to pay or enter a discount code, on top of the card-testing protection every store has. Turn on the Payment Rate Limits feature."],
            IsDone = rateLimitsOn && !behindUntrustedProxy,
            Status = behindUntrustedProxy
                ? S["This site is behind a proxy whose forwarded address isn't applied, so every buyer looks like the proxy and shares one limit, the always-on card-testing limits included. Turn on Reverse Proxy, forward X-Forwarded-For, and trust only your own proxies."]
                : rateLimitsOn
                    ? S["Payment Rate Limits is on. See Tools › Rate Limits."]
                    : S["Payment Rate Limits is off."],
            Url = _linkGenerator.GetPathByAction(httpContext, "Features", "Admin", new { area = OrchardCoreFeatureIds.Features }),
            UrlText = S["Features"],
        };
    }
}
