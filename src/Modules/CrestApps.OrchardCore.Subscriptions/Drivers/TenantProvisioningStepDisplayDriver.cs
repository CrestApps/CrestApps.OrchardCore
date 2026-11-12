using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Collects the details of the site a customer is buying, and refuses a name that is already taken before
/// they are asked to pay.
/// </summary>
/// <remarks>
/// Validating here rather than at completion is the whole reason this step exists as a separate stage.
/// Telling somebody their chosen site name is taken while they can still change it is a small inconvenience;
/// telling them after their card has been charged is a refund, an apology, and a support ticket.
///
/// The administrator password is data-protected before it is written to the session and never rendered back
/// into the form, so a password the customer typed is not sitting in plain text in the tenant database or in
/// the HTML of a page they might leave open.
/// </remarks>
public sealed class TenantProvisioningStepDisplayDriver : CheckoutFlowDisplayDriver
{
    private readonly IShellHost _shellHost;
    private readonly ISiteService _siteService;
    private readonly ITenantPurchaseRulesProvider _rulesProvider;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly DocumentJsonSerializerOptions _jsonOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host used to check availability.</param>
    /// <param name="setupService">The setup service used to list the site templates on offer.</param>
    /// <param name="dataProtectionProvider">The provider used to protect the administrator password.</param>
    /// <param name="jsonOptions">The serializer options used for persisted step data.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TenantProvisioningStepDisplayDriver(
        IShellHost shellHost,
        ISiteService siteService,
        ITenantPurchaseRulesProvider rulesProvider,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        IStringLocalizer<TenantProvisioningStepDisplayDriver> stringLocalizer)
    {
        _shellHost = shellHost;
        _siteService = siteService;
        _rulesProvider = rulesProvider;
        _dataProtectionProvider = dataProtectionProvider;
        _jsonOptions = jsonOptions.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string StepKey
        => SubscriptionConstants.StepKey.TenantProvisioning;

    /// <inheritdoc/>
    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        var saved = ReadSaved(flow);
        var onboarding = await _siteService.GetSettingsAsync<SubscriptionOnboardingSettings>();

        return Initialize<TenantProvisioningStepViewModel>("TenantProvisioningStep", model =>
        {
            // Where the site answers is the operator's choice unless they let buyers choose.
            model.AllowCustomPrefix = onboarding.AllowCustomPrefix;
            model.AllowCustomDomains = onboarding.AllowCustomDomains;

            model.TenantName = saved?.TenantName;
            model.TenantTitle = saved?.TenantTitle;
            model.Prefix = saved?.Prefix;
            model.Domains = saved?.Domains is { Length: > 0 } ? string.Join(", ", saved.Domains) : null;
            model.AdminUsername = saved?.AdminUsername;
            model.AdminEmail = saved?.AdminEmail;

            // A password is never rendered back. The customer retypes it if they revisit the step, which is
            // the same trade every setup form makes.
            model.HasPassword = ProtectedSettingExtensions.HasSecret(saved?.ProtectedAdminPassword);
        }).Location("Content:5");
    }

    /// <inheritdoc/>
    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        var model = new TenantProvisioningStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var saved = ReadSaved(flow);

        // The recipe and the feature profile come from the plan, never from the form: a buyer who could pick any setup
        // recipe on the host, or none of the plan's limits, would get a site the operator never offered.
        var planRecipe = GetStepData(flow, "RecipeName");
        var rules = await _rulesProvider.GetRulesAsync(planRecipe);
        var request = TenantPurchaseValidator.Normalize(new TenantPurchaseRequest
        {
            TenantName = model.TenantName,
            Prefix = rules.AllowCustomPrefix ? model.Prefix : null,
            Domains = rules.AllowCustomDomains ? SplitDomains(model.Domains) : [],
            RecipeName = planRecipe,
            FeatureProfile = GetStepData(flow, "FeatureProfile"),
        }, rules);

        foreach (var error in TenantPurchaseValidator.Validate(request, rules, _shellHost.GetAllSettings()))
        {
            var field = error.Field switch
            {
                nameof(TenantPurchaseRequest.Domains) => nameof(model.Domains),
                nameof(TenantPurchaseRequest.Prefix) => nameof(model.Prefix),
                _ => nameof(model.TenantName),
            };

            context.Updater.ModelState.AddModelError(Prefix, field, GetMessage(error));
        }

        var tenantName = request.TenantName;

        if (string.IsNullOrWhiteSpace(model.AdminUsername))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdminUsername), S["An administrator user name is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.AdminEmail))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdminEmail), S["An administrator email address is required."]);
        }

        // A password already captured on a previous visit stays valid, so the customer is not forced to
        // retype it every time they step back through the checkout.
        var hasExistingPassword = ProtectedSettingExtensions.HasSecret(saved?.ProtectedAdminPassword);

        if (string.IsNullOrEmpty(model.AdminPassword) && !hasExistingPassword)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdminPassword), S["An administrator password is required."]);
        }
        else if (!string.IsNullOrEmpty(model.AdminPassword) && model.AdminPassword != model.ConfirmPassword)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConfirmPassword), S["The passwords do not match."]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            var step = new TenantProvisioningStep
            {
                TenantName = tenantName,
                TenantTitle = string.IsNullOrWhiteSpace(model.TenantTitle) ? tenantName : model.TenantTitle.Trim(),
                Prefix = request.Prefix,
                Domains = request.Domains,
                AdminUsername = model.AdminUsername?.Trim(),
                AdminEmail = model.AdminEmail?.Trim(),
                RecipeName = request.RecipeName,
                FeatureProfile = request.FeatureProfile,

                // Protecting it here, at the moment it is captured, means the plain password never reaches
                // the session store even briefly.
                ProtectedAdminPassword = string.IsNullOrEmpty(model.AdminPassword)
                    ? saved?.ProtectedAdminPassword
                    : _dataProtectionProvider.ProtectSetting(SubscriptionConstants.ProtectorPurposes.TenantOnboardingStep, model.AdminPassword),
            };

            flow.Session.SavedSteps[StepKey] = JsonSerializer.SerializeToNode(step, _jsonOptions.SerializerOptions);
        }

        return await EditStepAsync(flow, context);
    }

    private TenantProvisioningStep ReadSaved(CheckoutFlow flow)
    {
        if (!flow.Session.SavedSteps.TryGetPropertyValue(StepKey, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.Deserialize<TenantProvisioningStep>(_jsonOptions.SerializerOptions);
        }
        catch (JsonException)
        {
            // Unreadable saved data is treated as absent so the customer can simply fill the step in again.
            return null;
        }
    }

    private static string GetStepData(CheckoutFlow flow, string key)
    {
        var step = flow.Session.Steps.FirstOrDefault(x => x.Key == SubscriptionConstants.StepKey.TenantProvisioning);

        if (step is not null && step.Data.TryGetValue(key, out var value))
        {
            return value switch
            {
                string text => text,
                JsonNode node => node.GetValue<string>(),
                _ => value?.ToString(),
            };
        }

        return null;
    }

    private static string[] SplitDomains(string value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private string GetMessage(TenantPurchaseError error)
        => error.Code switch
        {
            TenantPurchaseErrorCode.NameRequired => S["Choose a name for your site."],
            TenantPurchaseErrorCode.InvalidName => S["The site name may only contain letters, digits, and underscores, and must start with a letter."],
            TenantPurchaseErrorCode.NameTaken => S["That site name is already taken."],
            TenantPurchaseErrorCode.PrefixRequired => S["Choose a URL prefix for your site."],
            TenantPurchaseErrorCode.InvalidPrefix => S["The URL prefix may only contain letters, digits, hyphens and underscores."],
            TenantPurchaseErrorCode.ReservedPrefix => S["The URL prefix '{0}' is reserved. Choose another.", error.Value],
            TenantPurchaseErrorCode.PrefixTaken => S["That URL prefix belongs to another site."],
            TenantPurchaseErrorCode.PrefixNotAllowed => S["The URL prefix is set from the site name."],
            TenantPurchaseErrorCode.CustomDomainsNotAllowed => S["This store doesn't offer custom domains."],
            TenantPurchaseErrorCode.InvalidHost => S["'{0}' is not a domain name a site can use.", error.Value],
            TenantPurchaseErrorCode.ReservedHost => S["The domain '{0}' is reserved.", error.Value],
            TenantPurchaseErrorCode.HostTaken => S["The domain '{0}' belongs to another site.", error.Value],
            TenantPurchaseErrorCode.RecipeNotAllowed => S["This plan can't be bought yet: the store hasn't finished setting it up."],
            TenantPurchaseErrorCode.FeatureProfileRequired or TenantPurchaseErrorCode.FeatureProfileNotFound => S["This plan can't be bought yet: the store hasn't finished setting it up."],
            _ => S["The details of your new site are not accepted."],
        };
}
