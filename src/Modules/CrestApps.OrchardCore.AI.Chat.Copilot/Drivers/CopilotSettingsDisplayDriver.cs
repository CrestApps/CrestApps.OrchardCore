using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;

public sealed class CopilotSettingsDisplayDriver : SiteDisplayDriver<CopilotSettings>
{
    private const string ProtectorPurpose = "CrestApps.OrchardCore.AI.Chat.Copilot.Settings";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly CopilotCallbackUrlProvider _callbackUrlProvider;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CopilotSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        CopilotCallbackUrlProvider callbackUrlProvider,
        IHtmlLocalizer<CopilotSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<CopilotSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _callbackUrlProvider = callbackUrlProvider;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public override IDisplayResult Edit(ISite site, CopilotSettings settings, BuildEditorContext context)
    {
        return Initialize<CopilotSettingsViewModel>("CopilotSettings_Edit", async model =>
        {
            model.AuthenticationType = settings.AuthenticationType;
            model.ClientId = settings.ClientId;
            model.HasSecret = !string.IsNullOrWhiteSpace(settings.ProtectedClientSecret);
            model.ComputedCallbackUrl = await _callbackUrlProvider.GetCallbackUrlAsync();

            // BYOK fields
            model.ProviderType = settings.ProviderType;
            model.BaseUrl = settings.BaseUrl;
            model.HasApiKey = !string.IsNullOrWhiteSpace(settings.ProtectedApiKey);
            model.WireApi = settings.WireApi ?? "completions";
            model.DefaultModel = settings.DefaultModel;
            model.AzureApiVersion = settings.AzureApiVersion;

            // Select list options
            model.AuthenticationTypes =
            [
                new SelectListItem(S["Not configured"], nameof(CopilotAuthenticationType.NotConfigured)),
                new SelectListItem(S["GitHub signed-in user"], nameof(CopilotAuthenticationType.GitHubOAuth)),
                new SelectListItem(S["API key (BYOK)"], nameof(CopilotAuthenticationType.ApiKey)),
            ];

            model.ProviderTypes =
            [
                new SelectListItem(S["OpenAI / OpenAI-compatible (Ollama, vLLM, etc.)"], "openai"),
                new SelectListItem(S["Azure OpenAI"], "azure"),
                new SelectListItem(S["Anthropic"], "anthropic"),
            ];

            model.WireApiOptions =
            [
                new SelectListItem(S["Chat completions (default)"], "completions"),
                new SelectListItem(S["Responses (GPT-5 series)"], "responses"),
            ];
        })
        .Location("Content:8%Copilot;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CopilotPermissionProvider.ManageCopilotSettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, CopilotSettings settings, UpdateEditorContext context)
    {
        var model = new CopilotSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.AuthenticationType = model.AuthenticationType;

        if (settings.AuthenticationType == CopilotAuthenticationType.NotConfigured)
        {
            return await EditAsync(site, settings, context);
        }

        if (settings.AuthenticationType == CopilotAuthenticationType.GitHubOAuth)
        {
            // GitHub OAuth validation
            settings.ClientId = model.ClientId;

            if (string.IsNullOrWhiteSpace(settings.ClientId))
            {
                context.Updater.ModelState.AddModelError(nameof(model.ClientId), S["Client ID is required."]);
            }

            if (!string.IsNullOrWhiteSpace(model.ClientSecret))
            {
                var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
                settings.ProtectedClientSecret = protector.Protect(model.ClientSecret);
            }
            else if (string.IsNullOrWhiteSpace(settings.ProtectedClientSecret))
            {
                context.Updater.ModelState.AddModelError(nameof(model.ClientSecret), S["Client secret is required."]);
            }
        }
        else
        {
            // BYOK (API Key) validation
            settings.ProviderType = model.ProviderType;
            settings.BaseUrl = model.BaseUrl;
            settings.WireApi = model.WireApi;
            settings.DefaultModel = model.DefaultModel;
            settings.AzureApiVersion = model.AzureApiVersion;

            if (string.IsNullOrWhiteSpace(settings.ProviderType))
            {
                context.Updater.ModelState.AddModelError(nameof(model.ProviderType), S["Provider type is required."]);
            }

            if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                context.Updater.ModelState.AddModelError(nameof(model.BaseUrl), S["Base URL is required."]);
            }

            if (string.IsNullOrWhiteSpace(settings.DefaultModel))
            {
                context.Updater.ModelState.AddModelError(nameof(model.DefaultModel), S["Default model is required."]);
            }

            if (!string.IsNullOrWhiteSpace(model.ApiKey))
            {
                var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
                settings.ProtectedApiKey = protector.Protect(model.ApiKey);
            }

            if (string.Equals(settings.ProviderType, "azure", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(settings.AzureApiVersion))
            {
                context.Updater.ModelState.AddModelError(nameof(model.AzureApiVersion), S["Azure API version is required for Azure provider."]);
            }

            if (string.Equals(settings.ProviderType, "azure", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(model.ApiKey)
                && string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
            {
                context.Updater.ModelState.AddModelError(nameof(model.ApiKey), S["API key is required for Azure provider."]);
            }
        }

        return await EditAsync(site, settings, context);
    }
}
