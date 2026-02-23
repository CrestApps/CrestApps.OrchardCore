using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
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
    private readonly LinkGenerator _linkGenerator;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CopilotSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        LinkGenerator linkGenerator,
        IHtmlLocalizer<CopilotSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<CopilotSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _linkGenerator = linkGenerator;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public override IDisplayResult Edit(ISite site, CopilotSettings settings, BuildEditorContext context)
    {
        return Initialize<CopilotSettingsViewModel>("CopilotSettings_Edit", model =>
        {
            model.AuthenticationType = settings.AuthenticationType;
            model.ClientId = settings.ClientId;
            model.HasSecret = !string.IsNullOrWhiteSpace(settings.ProtectedClientSecret);
            model.ComputedCallbackUrl = _linkGenerator.GetUriByAction(_httpContextAccessor.HttpContext, "OAuthCallback", "CopilotAuth", new
            {
                area = "CrestApps.OrchardCore.AI.Chat.Copilot",
            });

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
                new SelectListItem(S["GitHub Signed-in User"], nameof(CopilotAuthenticationType.GitHubOAuth)),
                new SelectListItem(S["API Key (BYOK)"], nameof(CopilotAuthenticationType.ApiKey)),
            ];

            model.ProviderTypes =
            [
                new SelectListItem(S["OpenAI / OpenAI-compatible (Ollama, vLLM, etc.)"], "openai"),
                new SelectListItem(S["Azure OpenAI"], "azure"),
                new SelectListItem(S["Anthropic"], "anthropic"),
            ];

            model.WireApiOptions =
            [
                new SelectListItem(S["Chat Completions (default)"], "completions"),
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
                context.Updater.ModelState.AddModelError(nameof(model.ClientSecret), S["Client Secret is required."]);
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
                context.Updater.ModelState.AddModelError(nameof(model.ProviderType), S["Provider Type is required."]);
            }

            if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                context.Updater.ModelState.AddModelError(nameof(model.BaseUrl), S["Base URL is required."]);
            }

            if (string.IsNullOrWhiteSpace(settings.DefaultModel))
            {
                context.Updater.ModelState.AddModelError(nameof(model.DefaultModel), S["Default Model is required."]);
            }

            if (!string.IsNullOrWhiteSpace(model.ApiKey))
            {
                var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
                settings.ProtectedApiKey = protector.Protect(model.ApiKey);
            }

            if (string.Equals(settings.ProviderType, "azure", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(settings.AzureApiVersion))
            {
                context.Updater.ModelState.AddModelError(nameof(model.AzureApiVersion), S["Azure API Version is required for Azure provider."]);
            }

            if (string.Equals(settings.ProviderType, "azure", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(model.ApiKey)
                && string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
            {
                context.Updater.ModelState.AddModelError(nameof(model.ApiKey), S["API Key is required for Azure provider."]);
            }
        }

        return await EditAsync(site, settings, context);
    }
}
