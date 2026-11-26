using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.Plaid.Services;
using CrestApps.OrchardCore.Plaid.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Secrets;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Plaid.Drivers;

/// <summary>
/// The Plaid settings page: the environment, the client id and which Orchard secret holds the Plaid secret. The secret
/// itself is never on this page. When the host supplies the keys (a provider registered with
/// <c>AddPlaidCredentialsProvider</c>), the page says so instead, and saving it changes nothing.
/// </summary>
internal sealed class PlaidSettingsDisplayDriver : SiteDisplayDriver<PlaidSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISecretManager _secretManager;
    private readonly IPlaidCredentialsProvider _credentialsProvider;
    private readonly IPlaidLinkSite _linkSite;

    internal readonly IStringLocalizer S;

    public PlaidSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ISecretManager secretManager,
        IPlaidCredentialsProvider credentialsProvider,
        IPlaidLinkSite linkSite,
        IStringLocalizer<PlaidSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _secretManager = secretManager;
        _credentialsProvider = credentialsProvider;
        _linkSite = linkSite;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => PlaidConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, PlaidSettings settings, BuildEditorContext context)
        => Combine(
            Initialize<PlaidSettingsViewModel>("PlaidSettings_Edit", async model =>
            {
                await PopulateStateAsync(model, settings);

                if (model.HostSuppliesKeys)
                {
                    return;
                }

                model.Environment = settings.Environment;
                model.ClientId = settings.ClientId;
                model.SecretName = string.IsNullOrEmpty(settings.SecretName) ? PlaidConstants.DefaultSecretName : settings.SecretName;
                model.Environments =
                [
                    new SelectListItem(S["Sandbox (test banks)"], nameof(PlaidEnvironment.Sandbox)),
                    new SelectListItem(S["Production (real banks)"], nameof(PlaidEnvironment.Production)),
                ];
                model.Secrets = await GetTextSecretsAsync(model.SecretName);
            })
                .Location(l => l.Zone("Content", "5").Card(S["Credentials"], "10"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PlaidPermissions.ManagePlaidSettings))
                .OnGroup(SettingsGroupId),
            Initialize<PlaidSettingsViewModel>("PlaidSettingsWebhook_Edit", async model => await PopulateStateAsync(model, settings))
                .Location(l => l.Zone("Content", "6").Card(S["Webhook"], "20"))
                .RenderWhen(async () => !string.IsNullOrEmpty(await _linkSite.GetWebhookUrlAsync())
                    && await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PlaidPermissions.ManagePlaidSettings))
                .OnGroup(SettingsGroupId),
            Initialize<PlaidSettingsViewModel>("PlaidSettingsTest_Edit", async model => await PopulateStateAsync(model, settings))
                .Location(l => l.Zone("Content", "7").Card(S["Connection Test"], "30"))
                .RenderWhen(async () => await IsConfiguredAsync(settings)
                    && await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PlaidPermissions.ManagePlaidSettings))
                .OnGroup(SettingsGroupId));

    // What the page says about the connection, whichever card is drawn.
    private async Task PopulateStateAsync(PlaidSettingsViewModel model, PlaidSettings settings)
    {
        model.WebhookUrl = await _linkSite.GetWebhookUrlAsync();

        if (HostSuppliesKeys(_credentialsProvider))
        {
            model.HostSuppliesKeys = true;
            model.IsConfigured = await _credentialsProvider.GetAsync() is not null;

            return;
        }

        model.IsConfigured = settings.IsConfigured();
    }

    private async Task<bool> IsConfiguredAsync(PlaidSettings settings)
        => HostSuppliesKeys(_credentialsProvider)
            ? await _credentialsProvider.GetAsync() is not null
            : settings.IsConfigured();

    public override async Task<IDisplayResult> UpdateAsync(ISite site, PlaidSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PlaidPermissions.ManagePlaidSettings))
        {
            return null;
        }

        // The host's keys are used whatever the settings hold, so they are left as they are.
        if (HostSuppliesKeys(_credentialsProvider))
        {
            return Edit(site, settings, context);
        }

        var model = new PlaidSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Environment = model.Environment;
        settings.ClientId = model.ClientId?.Trim();
        settings.SecretName = model.SecretName?.Trim();

        if (!string.IsNullOrEmpty(settings.ClientId) && string.IsNullOrEmpty(settings.SecretName))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.SecretName), S["Choose the secret that holds the Plaid secret."]);
        }

        return Edit(site, settings, context);
    }

    private async Task<List<SelectListItem>> GetTextSecretsAsync(string selected)
    {
        var items = new List<SelectListItem>();

        foreach (var info in await _secretManager.GetSecretInfosAsync())
        {
            if (IsTextSecret(info.Type))
            {
                items.Add(new SelectListItem(info.Name, info.Name, string.Equals(info.Name, selected, StringComparison.Ordinal)));
            }
        }

        // A name typed earlier, or the default, shows even before the secret exists.
        if (!string.IsNullOrEmpty(selected) && !items.Any(item => item.Value == selected))
        {
            items.Insert(0, new SelectListItem(S["{0} (not created yet)", selected], selected, selected: true));
        }

        return items.OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Returns whether the keys come from the host rather than these settings: another provider replaced the
    /// settings-based one.
    /// </summary>
    internal static bool HostSuppliesKeys(IPlaidCredentialsProvider provider)
        => provider is not PlaidCredentialsProvider;

    /// <summary>
    /// Returns whether a secret's stored type is a text secret. The store records the full type name
    /// (<c>OrchardCore.Secrets.TextSecret</c>); the short name is accepted too.
    /// </summary>
    internal static bool IsTextSecret(string type)
        => string.Equals(type, typeof(TextSecret).FullName, StringComparison.Ordinal)
            || string.Equals(type, nameof(TextSecret), StringComparison.Ordinal);
}
