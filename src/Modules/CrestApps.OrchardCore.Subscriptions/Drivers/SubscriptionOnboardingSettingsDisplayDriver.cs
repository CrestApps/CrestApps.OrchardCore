using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class SubscriptionOnboardingSettingsDisplayDriver : SiteDisplayDriver<SubscriptionOnboardingSettings>
{
    internal readonly IStringLocalizer S;

    public SubscriptionOnboardingSettingsDisplayDriver(IStringLocalizer<SubscriptionOnboardingSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => SubscriptionSettingsDisplayDriver.GroupId;

    public override IDisplayResult Edit(ISite model, SubscriptionOnboardingSettings settings, BuildEditorContext context)
    {
        return Initialize<SubscriptionOnboardingSettingsViewModel>("SubscriptionOnboardingSettings_Edit", model =>
        {
            model.AllowCustomDomains = settings.AllowCustomDomains;
            model.LocalDomainType = settings.LocalDomainType;
            model.LocalDomainTemplate = settings.LocalDomainTemplate;
            model.LocalDomainTypes =
            [
                new SelectListItem(S["None"], nameof(LocalDomainType.None)),
                new SelectListItem(S["Generate a hidden local domain"], nameof(LocalDomainType.GeneratedHidden)),
                new SelectListItem(S["Generate a visible local domain"], nameof(LocalDomainType.Generated)),
                new SelectListItem(S["Set the tenant name as a prefix"], nameof(LocalDomainType.Prefix)),
            ];
        }).Location("Content:5#Tenant Onboarding:5")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, SubscriptionOnboardingSettings settings, UpdateEditorContext context)
    {
        var model = new SubscriptionOnboardingSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!model.AllowCustomDomains && model.LocalDomainType == LocalDomainType.None)
        {
            context.Updater.ModelState.AddModelError(string.Empty, S["At least one options for domains should be set."]);
        }
        else if (model.LocalDomainType != LocalDomainType.None && model.LocalDomainType != LocalDomainType.Prefix)
        {
            var tenantNamePlaceholder = Guid.NewGuid().ToString("n");
            var domainPlaceholder = Guid.NewGuid().ToString("n");

            if (string.IsNullOrWhiteSpace(model.LocalDomainTemplate))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.LocalDomainTemplate), S["Domain template is required."]);
            }
            else if (!model.LocalDomainTemplate.Contains(SubscriptionOnboardingSettings.TenantKeyVariable))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.LocalDomainTemplate), S["Domain template must include the variable: {0}.", SubscriptionOnboardingSettings.TenantKeyVariable]);
            }
            else
            {
                var templates = model.LocalDomainTemplate
                    .Replace(SubscriptionOnboardingSettings.TenantKeyVariable, tenantNamePlaceholder)
                    .Replace(SubscriptionOnboardingSettings.CurrentHostVariable, domainPlaceholder)
                    .ToLowerInvariant()
                    .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var isValid = templates.Length > 0;

                var validatedHosts = new HashSet<string>();

                foreach (var template in templates)
                {
                    if (!Uri.TryCreate(GetLink(template), UriKind.Absolute, out var validatedHost))
                    {
                        context.Updater.ModelState.AddModelError(Prefix, nameof(model.LocalDomainTemplate), S["Invalid template provided. The template must be a valid domain format."]);
                        isValid = false;
                        break;
                    }
                    else
                    {
                        var validatedValue = validatedHost.Host
                            .Replace(tenantNamePlaceholder, SubscriptionOnboardingSettings.TenantKeyVariable)
                            .Replace(domainPlaceholder, SubscriptionOnboardingSettings.CurrentHostVariable);

                        validatedHosts.Add(validatedValue);
                    }
                }

                if (isValid)
                {
                    settings.LocalDomainTemplate = string.Join(',', validatedHosts);
                }
                else
                {
                    settings.LocalDomainTemplate = model.LocalDomainTemplate;
                }
            }
        }

        settings.AllowCustomDomains = model.AllowCustomDomains;
        settings.LocalDomainType = model.LocalDomainType;

        return Edit(site, settings, context);
    }

    private static string GetLink(string domain)
    {
        if (!domain.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return $"http://{domain}";
        }

        return domain;
    }
}
