using CrestApps.OrchardCore.Core.Security;
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
        => Initialize<PlaidSettingsViewModel>("PlaidSettings_Edit", async model =>
            {
                model.WebhookUrl = await _linkSite.GetWebhookUrlAsync();

                if (HostSuppliesKeys(_credentialsProvider))
                {
                    model.HostSuppliesKeys = true;
                    model.IsConfigured = await _credentialsProvider.GetAsync() is not null;

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
                model.Secrets = await TextSecretSelectList.BuildAsync(_secretManager, model.SecretName, S);
                model.IsConfigured = settings.IsConfigured();
            })
            .Location("Content:5")
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PlaidPermissions.ManagePlaidSettings))
            .OnGroup(SettingsGroupId);

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

    /// <summary>
    /// Returns whether the keys come from the host rather than these settings: another provider replaced the
    /// settings-based one.
    /// </summary>
    internal static bool HostSuppliesKeys(IPlaidCredentialsProvider provider)
        => provider is not PlaidCredentialsProvider;
}
