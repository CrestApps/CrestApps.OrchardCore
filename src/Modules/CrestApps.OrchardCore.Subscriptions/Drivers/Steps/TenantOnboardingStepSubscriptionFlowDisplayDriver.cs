using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.DataProtection;
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
    public const string ProtectorPurpose = "TenantOnboardingStep";
    public const string KeyNameRegexPattern = @"^[a-zA-Z][a-zA-Z0-9_-]*$";

    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;
    private readonly IShellHost _shellHost;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public TenantOnboardingStepSubscriptionFlowDisplayDriver(
        IShellHost shellHost,
        ISiteService siteService,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<TenantOnboardingStepSubscriptionFlowDisplayDriver> stringLocalizer)
    {
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        _shellHost = shellHost;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _dataProtectionProvider = dataProtectionProvider;
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

            if (TryGetStepInfo(flow.Session, out var stepInfo))
            {
                PopulateViewModel(model, stepInfo);
            }
        }).Location("Content");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        var model = new TenantOnboardingStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (TryGetStepInfo(flow.Session, out var stepInfo))
        {
            PopulateViewModel(model, stepInfo);
        }

        stepInfo ??= new TenantOnboardingStep();

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

        ValidateAndProtectPassword(context.Updater, model, stepInfo);

        stepInfo.TenantName = model.TenantName;
        stepInfo.TenantTitle = model.TenantTitle;
        stepInfo.AdminEmail = model.AdminEmail;
        stepInfo.AdminUsername = model.AdminUsername;

        var settings = await _siteService.GetSettingsAsync<SubscriptionOnboardingSettings>();

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

    private void ValidateAndProtectPassword(IUpdateModel updater, TenantOnboardingStepViewModel model, TenantOnboardingStep stepInfo)
    {
        var validatePassword =
            string.IsNullOrEmpty(stepInfo.AdminPassword) ||
            !string.IsNullOrEmpty(model.AdminPassword) ||
            !string.IsNullOrEmpty(model.AdminPasswordConfirmation);

        if (!validatePassword)
        {
            return;
        }

        var hasValidPassword = true;

        if (string.IsNullOrEmpty(model.AdminPassword))
        {
            hasValidPassword = false;
            updater.ModelState.AddModelError(Prefix, nameof(model.AdminPassword), S["Password is a required value"]);
        }

        if (string.IsNullOrEmpty(model.AdminPasswordConfirmation))
        {
            hasValidPassword = false;
            updater.ModelState.AddModelError(Prefix, nameof(model.AdminPassword), S["Password Confirmation is a required value"]);
        }

        if (model.AdminPassword != model.AdminPasswordConfirmation)
        {
            hasValidPassword = false;
            updater.ModelState.AddModelError(Prefix, nameof(model.AdminPasswordConfirmation), S["Password and Password Confirmation values must be the same."]);
        }

        if (hasValidPassword)
        {
            var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
            stepInfo.AdminPassword = protector.Protect(model.AdminPassword);
        }
    }

    private bool TryGetStepInfo(ISubscriptionFlowSession session, out TenantOnboardingStep stepInfo)
    {
        if (!session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantOnboarding, out var node))
        {
            stepInfo = null;

            return false;
        }

        stepInfo = node.Deserialize<TenantOnboardingStep>(_documentJsonSerializerOptions.SerializerOptions);

        return true;
    }


    private static void PopulateViewModel(TenantOnboardingStepViewModel model, TenantOnboardingStep stepInfo)
    {
        model.TenantName = stepInfo.TenantName;
        model.TenantTitle = stepInfo.TenantTitle;
        model.AdminUsername = stepInfo.AdminUsername;
        model.AdminEmail = stepInfo.AdminEmail;
        model.HasSavedPassword = !string.IsNullOrEmpty(stepInfo.AdminPassword);
        model.DomainName = stepInfo.Domains?.Length > 0 ? string.Join(',', stepInfo.Domains) : null;
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
