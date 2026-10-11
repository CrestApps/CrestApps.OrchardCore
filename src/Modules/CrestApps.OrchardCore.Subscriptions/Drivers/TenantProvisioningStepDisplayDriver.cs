using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
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
using OrchardCore.Setup.Services;

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
    private readonly ISetupService _setupService;
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
        ISetupService setupService,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        IStringLocalizer<TenantProvisioningStepDisplayDriver> stringLocalizer)
    {
        _shellHost = shellHost;
        _setupService = setupService;
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
        var recipes = await _setupService.GetSetupRecipesAsync();

        return Initialize<TenantProvisioningStepViewModel>("TenantProvisioningStep", model =>
        {
            model.TenantName = saved?.TenantName;
            model.TenantTitle = saved?.TenantTitle;
            model.Prefix = saved?.Prefix;
            model.Domains = saved?.Domains is { Length: > 0 } ? string.Join(", ", saved.Domains) : null;
            model.AdminUsername = saved?.AdminUsername;
            model.AdminEmail = saved?.AdminEmail;
            model.RecipeName = saved?.RecipeName ?? GetStepData(flow, "RecipeName");

            // A password is never rendered back. The customer retypes it if they revisit the step, which is
            // the same trade every setup form makes.
            model.HasPassword = !string.IsNullOrEmpty(saved?.ProtectedAdminPassword);

            model.Recipes = [.. recipes
                .Where(recipe => !string.IsNullOrEmpty(recipe.Name))
                .Select(recipe => new TenantRecipeOption
                {
                    Name = recipe.Name,
                    DisplayName = string.IsNullOrEmpty(recipe.DisplayName) ? recipe.Name : recipe.DisplayName,
                    Description = recipe.Description,
                })];
        }).Location("Content:5");
    }

    /// <inheritdoc/>
    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        var model = new TenantProvisioningStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var saved = ReadSaved(flow);
        var tenantName = model.TenantName?.Trim();

        if (string.IsNullOrEmpty(tenantName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TenantName), S["Choose a name for your site."]);
        }
        else if (!IsValidTenantName(tenantName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TenantName), S["The site name may only contain letters, digits, and underscores, and must start with a letter."]);
        }
        else if (_shellHost.TryGetSettings(tenantName, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TenantName), S["That site name is already taken."]);
        }

        var domains = SplitDomains(model.Domains);
        var allSettings = _shellHost.GetAllSettings();

        if (domains.Length > 0 && allSettings.Any(settings => settings.HasUrlHost(domains)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Domains), S["One of those domains belongs to another site."]);
        }

        var prefix = model.Prefix?.Trim();

        if (!string.IsNullOrEmpty(prefix) && allSettings.Any(settings => settings.HasUrlPrefix(prefix)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Prefix), S["That URL prefix belongs to another site."]);
        }

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
        var hasExistingPassword = !string.IsNullOrEmpty(saved?.ProtectedAdminPassword);

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
                Prefix = prefix,
                Domains = domains,
                AdminUsername = model.AdminUsername?.Trim(),
                AdminEmail = model.AdminEmail?.Trim(),
                RecipeName = model.RecipeName,
                FeatureProfile = GetStepData(flow, "FeatureProfile"),

                // Protecting it here, at the moment it is captured, means the plain password never reaches
                // the session store even briefly.
                ProtectedAdminPassword = string.IsNullOrEmpty(model.AdminPassword)
                    ? saved?.ProtectedAdminPassword
                    : _dataProtectionProvider
                        .CreateProtector(SubscriptionConstants.ProtectorPurposes.TenantOnboardingStep)
                        .Protect(model.AdminPassword),
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

    // Orchard Core uses the tenant name for shell folders and table prefixes, so anything outside this set
    // produces a tenant that cannot be created rather than one that is merely oddly named.
    private static bool IsValidTenantName(string name)
    {
        if (!char.IsLetter(name[0]))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
