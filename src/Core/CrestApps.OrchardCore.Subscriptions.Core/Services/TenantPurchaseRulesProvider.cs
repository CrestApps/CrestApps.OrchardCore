using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
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

    /// <summary>
    /// Returns the recipe the plan named when it was bought, read from the checkout's new-site step, which the server
    /// wrote from the plan; <see langword="null"/> when the checkout or its step can't be found.
    /// </summary>
    /// <param name="checkoutSessionId">The checkout the site was bought with.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<string> GetPlanRecipeNameAsync(string checkoutSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every reason the purchase is refused: <see cref="TenantPurchaseValidator.Validate"/>, and a prefix whose
    /// URLs this store already serves itself (the first segment of one of its routes or of one of its pages' paths).
    /// </summary>
    /// <param name="request">The normalized request.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="tenants">The settings of every tenant on the host.</param>
    /// <param name="ignoreTenant">A tenant to leave out of the uniqueness checks.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<TenantPurchaseError>> ValidateAsync(
        TenantPurchaseRequest request,
        TenantPurchaseRules rules,
        IEnumerable<ShellSettings> tenants,
        string ignoreTenant = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the rules from <see cref="SubscriptionOnboardingSettings"/>, adding what is always reserved: the admin
/// prefix, the default tenant's host names, the site's base URL host, and the host the request arrived on (the
/// operator's own address); and the feature profiles that exist.
/// </summary>
public sealed class TenantPurchaseRulesProvider : ITenantPurchaseRulesProvider
{
    private readonly ISiteService _siteService;
    private readonly IShellHost _shellHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AdminOptions _adminOptions;
    private readonly ITenantFeatureProfileCatalog _featureProfiles;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly StoreRouteSegments _storeRoutes;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantPurchaseRulesProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service, which holds the onboarding settings.</param>
    /// <param name="shellHost">The shell host, which knows the default tenant's host names.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the host the request arrived on.</param>
    /// <param name="adminOptions">The admin options, for the admin prefix.</param>
    /// <param name="featureProfiles">The feature profiles a sold site may be given.</param>
    /// <param name="sessionStore">The checkouts, which record the plan's recipe.</param>
    /// <param name="storeRoutes">The URLs this store serves itself.</param>
    public TenantPurchaseRulesProvider(
        ISiteService siteService,
        IShellHost shellHost,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AdminOptions> adminOptions,
        ITenantFeatureProfileCatalog featureProfiles,
        ICheckoutSessionStore sessionStore,
        StoreRouteSegments storeRoutes)
    {
        _siteService = siteService;
        _shellHost = shellHost;
        _httpContextAccessor = httpContextAccessor;
        _adminOptions = adminOptions.Value;
        _featureProfiles = featureProfiles;
        _sessionStore = sessionStore;
        _storeRoutes = storeRoutes;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantPurchaseError>> ValidateAsync(
        TenantPurchaseRequest request,
        TenantPurchaseRules rules,
        IEnumerable<ShellSettings> tenants,
        string ignoreTenant = null,
        CancellationToken cancellationToken = default)
    {
        var errors = TenantPurchaseValidator.Validate(request, rules, tenants, ignoreTenant).ToList();
        var prefix = request.Prefix?.Trim().Trim('/');

        // A site on a prefix of the store's own address takes every URL under it. The reserved list can't name every route
        // a module adds, nor the store's own pages, so a prefix the store already answers on is refused here too.
        if (!string.IsNullOrEmpty(prefix) &&
            request.Domains is not { Length: > 0 } &&
            !errors.Any(error => error.Field == nameof(TenantPurchaseRequest.Prefix)) &&
            await _storeRoutes.IsTakenAsync(prefix, cancellationToken))
        {
            errors.Add(new TenantPurchaseError(nameof(TenantPurchaseRequest.Prefix), TenantPurchaseErrorCode.ReservedPrefix, prefix));
        }

        return errors;
    }

    /// <inheritdoc/>
    public async Task<TenantPurchaseRules> GetRulesAsync(string planRecipeName)
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var onboarding = site.GetOrCreate<SubscriptionOnboardingSettings>();

        var hosts = new List<string>();

        if (_shellHost.TryGetSettings(ShellSettings.DefaultShellName, out var defaultSettings))
        {
            hosts.AddRange(defaultSettings.RequestUrlHosts ?? []);
        }

        // The address the site is configured to answer on is the operator's own, whichever alias the buyer came through,
        // and background provisioning has no request to read a host from.
        if (Uri.TryCreate(site.BaseUrl, UriKind.Absolute, out var baseUrl) && !string.IsNullOrEmpty(baseUrl.Host))
        {
            hosts.Add(baseUrl.Host);
        }

        var requestHost = _httpContextAccessor.HttpContext?.Request.Host.Host;

        if (!string.IsNullOrWhiteSpace(requestHost))
        {
            hosts.Add(requestHost);
        }

        var rules = onboarding.ToRules(planRecipeName, _adminOptions.AdminUrlPrefix, hosts);

        rules.KnownFeatureProfiles = await _featureProfiles.GetProfileIdsAsync();

        return rules;
    }

    /// <inheritdoc/>
    public async Task<string> GetPlanRecipeNameAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(checkoutSessionId) ||
            await _sessionStore.GetAsync(checkoutSessionId, cancellationToken) is not { } session)
        {
            return null;
        }

        var step = session.Steps?.FirstOrDefault(candidate => candidate.Key == SubscriptionConstants.StepKey.TenantProvisioning);

        return step is not null && step.Data.TryGetValue("RecipeName", out var value)
            ? value switch
            {
                string text => text,
                JsonNode node => node.GetValue<string>(),
                _ => value?.ToString(),
            }
            : null;
    }
}
