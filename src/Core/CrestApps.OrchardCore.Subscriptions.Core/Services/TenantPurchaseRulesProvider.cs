using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Builds the rules a tenant purchase is held to from the operator's onboarding settings.
/// </summary>
public interface ITenantPurchaseRulesProvider
{
    /// <summary>
    /// Gets the rules for a purchase of a plan.
    /// </summary>
    /// <param name="planRecipeName">The recipe the plan names.</param>
    /// <returns>The rules.</returns>
    Task<TenantPurchaseRules> GetRulesAsync(string planRecipeName);
}

/// <summary>
/// Builds the rules from <see cref="SubscriptionOnboardingSettings"/>, adding what is always reserved: the admin
/// prefix, the default tenant's host names, and the host the request arrived on (the operator's own address).
/// </summary>
public sealed class TenantPurchaseRulesProvider : ITenantPurchaseRulesProvider
{
    private readonly ISiteService _siteService;
    private readonly IShellHost _shellHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AdminOptions _adminOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantPurchaseRulesProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service, which holds the onboarding settings.</param>
    /// <param name="shellHost">The shell host, which knows the default tenant's host names.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the host the request arrived on.</param>
    /// <param name="adminOptions">The admin options, for the admin prefix.</param>
    public TenantPurchaseRulesProvider(
        ISiteService siteService,
        IShellHost shellHost,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AdminOptions> adminOptions)
    {
        _siteService = siteService;
        _shellHost = shellHost;
        _httpContextAccessor = httpContextAccessor;
        _adminOptions = adminOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<TenantPurchaseRules> GetRulesAsync(string planRecipeName)
    {
        var onboarding = await _siteService.GetSettingsAsync<SubscriptionOnboardingSettings>();

        var hosts = new List<string>();

        if (_shellHost.TryGetSettings(ShellSettings.DefaultShellName, out var defaultSettings))
        {
            hosts.AddRange(defaultSettings.RequestUrlHosts ?? []);
        }

        var requestHost = _httpContextAccessor.HttpContext?.Request.Host.Host;

        if (!string.IsNullOrWhiteSpace(requestHost))
        {
            hosts.Add(requestHost);
        }

        return onboarding.ToRules(planRecipeName, _adminOptions.AdminUrlPrefix, hosts);
    }
}
