using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed partial class TenantOnboardingStepSubscriptionFlowDisplayDriver : SubscriptionFlowDisplayDriver
{
    public const string KeyNameRegexPattern = @"^[a-zA-Z][a-zA-Z0-9_-]*$";

    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;
    private readonly IShellHost _shellHost;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public TenantOnboardingStepSubscriptionFlowDisplayDriver(
        IShellHost shellHost,
        ISiteService siteService,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<TenantOnboardingStepSubscriptionFlowDisplayDriver> stringLocalizer)
    {
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        _shellHost = shellHost;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    protected override string StepKey
        => SubscriptionConstants.StepKey.TenantOnboarding;

    protected override IDisplayResult EditStep(SubscriptionFlow flow, BuildEditorContext context)
    {
        return Initialize<TenantOnboardingStepViewModel>("TenantOnboardingStep_Edit", async model =>
        {
            var settings = await _siteService.GetSettingsAsync<SubscriptionOnboardingSettings>();
            var request = _httpContextAccessor.HttpContext.Request;

            // Do not include trailing slash to the current host.
            var currentHost = request.Host.ToString().TrimEnd('/');

            if (settings.LocalDomainType == LocalDomainType.Prefix)
            {
                model.DomainsTemplate = $"{request.Scheme}//{currentHost}/{SubscriptionOnboardingSettings.TenantKeyVariable}";
            }
            else if (settings.LocalDomainType != LocalDomainType.None && settings.LocalDomainType != LocalDomainType.GeneratedHidden)
            {
                model.DomainsTemplate = settings.LocalDomainTemplate.Replace(SubscriptionOnboardingSettings.CurrentHostVariable, currentHost);
            }

            model.AllowCustomDomain = settings.AllowCustomDomains;

            if (flow.Session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantOnboarding, out var node))
            {
                var info = node.Deserialize<TenantOnboardingStep>(_documentJsonSerializerOptions.SerializerOptions);

                model.TenantName = info.TenantName;
                model.TenantTitle = info.TenantTitle;
                model.AdminUsername = info.AdminUsername;
                model.AdminEmail = info.AdminEmail;
                model.AdminPassword = info.AdminPassword;
                model.DomainName = info.Domains?.Length > 0 ? string.Join(',', info.Domains) : null;
            }
        }).Location("Content");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        var model = new TenantOnboardingStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // TODO: add settings to generate tenant name.
        if (string.IsNullOrEmpty(model.TenantName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TenantName), S["Site Key is a required value"]);
        }
        else if (TenantNameRegex().IsMatch(model.TenantName) == false)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TenantName), S["Site Key must start with a letter and can only contain letters, numbers, underscores, and hyphens."]);
        }
        else if (_shellHost.TryGetSettings(model.TenantName, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TenantName), S["Site Key is unavailable. Please choose another key."]);
        }

        if (string.IsNullOrEmpty(model.AdminUsername))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdminUsername), S["Username is a required value"]);
        }

        if (string.IsNullOrEmpty(model.AdminPassword))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdminPassword), S["Password is a required value"]);
        }

        if (string.IsNullOrEmpty(model.AdminPasswordConfirmation))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdminPassword), S["Password Confirmation is a required value"]);
        }
        if (model.AdminPassword != model.AdminPasswordConfirmation)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdminPasswordConfirmation), S["Password and Password Confirmation values must be the same."]);
        }

        var settings = await _siteService.GetSettingsAsync<SubscriptionOnboardingSettings>();

        var stepInfo = new TenantOnboardingStep
        {
            TenantName = model.TenantName,
            TenantTitle = model.TenantTitle,
            AdminUsername = model.AdminUsername,
            AdminEmail = model.AdminEmail,
            AdminPassword = model.AdminPassword,
        };

        if (settings.AllowCustomDomains)
        {
            if (settings.LocalDomainType == LocalDomainType.None && string.IsNullOrWhiteSpace(model.DomainName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DomainName), S["At least one domain name is required."]);
            }
            else
            {
                stepInfo.Domains = GetValidDomains(context.Updater, model.DomainName);
            }
        }

        if (settings.LocalDomainType == LocalDomainType.Prefix)
        {
            if (_shellHost.GetAllSettings().Any(settings => settings.HasUrlPrefix(model.TenantName)))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.TenantName), S["Site Key is unavailable. Please choose another key."]);
            }

            stepInfo.Prefix = model.TenantName;
        }
        else if (settings.LocalDomainType != LocalDomainType.None && !string.IsNullOrEmpty(settings.LocalDomainTemplate))
        {
            stepInfo.LocalDomains = GetLocalDomains(context.Updater, model, settings.LocalDomainTemplate);
        }

        flow.Session.SavedSteps[SubscriptionConstants.StepKey.TenantOnboarding] = JObject.FromObject(stepInfo);

        return EditStep(flow, context);
    }

    private string[] GetLocalDomains(IUpdateModel updater, TenantOnboardingStepViewModel model, string template)
    {
        var localDomains = template
            .Replace(SubscriptionOnboardingSettings.TenantKeyVariable, model.TenantName)
            .Replace(SubscriptionOnboardingSettings.CurrentHostVariable, _httpContextAccessor.HttpContext.Request.Host.ToString())
            .ToLowerInvariant()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var validatedHosts = new HashSet<string>();
        var isValid = localDomains.Length > 0;
        var shellSettings = _shellHost.GetAllSettings();

        foreach (var domain in localDomains)
        {
            if (!Uri.TryCreate(GetLink(domain), UriKind.Absolute, out var validUrl))
            {
                updater.ModelState.AddModelError(Prefix, nameof(model.DomainName), S["Invalid domain name provided."]);
                isValid = false;
                break;
            }

            if (validatedHosts.Contains(validUrl.Host))
            {
                continue;
            }

            if (shellSettings.Any(settings => settings.HasUrlHost(validUrl.Host)))
            {
                updater.ModelState.AddModelError(Prefix, nameof(model.TenantName), S["Site Key is unavailable. Please choose another key."]);
            }
            else
            {
                validatedHosts.Add(validUrl.Host);
            }
        }

        return isValid ? validatedHosts.ToArray() : [];
    }

    private string[] GetValidDomains(IUpdateModel updater, string template)
    {
        var domains = template.ToLowerInvariant()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var validatedHosts = new HashSet<string>();
        var isValid = domains.Length > 0;
        var shellSettings = _shellHost.GetAllSettings();

        foreach (var domain in domains)
        {
            if (!Uri.TryCreate(GetLink(domain), UriKind.Absolute, out var validUrl))
            {
                updater.ModelState.AddModelError(Prefix, nameof(TenantOnboardingStepViewModel.DomainName), S["Invalid domain name provided."]);
                isValid = false;
                break;
            }

            if (validatedHosts.Contains(validUrl.Host))
            {
                continue;
            }

            if (shellSettings.Any(settings => settings.HasUrlHost(validUrl.Host)))
            {
                updater.ModelState.AddModelError(Prefix, nameof(TenantOnboardingStepViewModel.DomainName), S["The domain '{0}' is unavailable.", validUrl.Host]);
            }
            else
            {
                validatedHosts.Add(validUrl.Host);
            }
        }

        return isValid ? validatedHosts.ToArray() : [];
    }

    private static string GetLink(string domain)
    {
        if (!domain.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return $"http://{domain}";
        }

        return domain;
    }

    [GeneratedRegex(KeyNameRegexPattern)]
    private static partial Regex TenantNameRegex();
}
