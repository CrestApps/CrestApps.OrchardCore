using System.Globalization;
using System.Text.RegularExpressions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Speech;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Mvc.Web.Areas.Admin.Models;
using CrestApps.Core.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Models;
using CrestApps.Core.Mvc.Web.Areas.ChatInteractions.Models;
using CrestApps.Core.Mvc.Web.Models;
using CrestApps.Core.Mvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class SettingsController : Controller
{
    private const string CopilotProtectorPurpose = "CrestApps.Core.Mvc.Web.CopilotSettings";
    private const string MemoryIndexProfileType = "AIMemory";

    private readonly AppDataSettingsService<GeneralAISettings> _settingsService;
    private readonly AppDataSettingsService<ChatInteractionSettings> _chatInteractionSettingsService;
    private readonly AppDataSettingsService<DefaultOrchestratorSettings> _defaultOrchestratorSettingsService;
    private readonly AppDataSettingsService<DefaultAIDeploymentSettings> _deploymentDefaultsService;
    private readonly AppDataSettingsService<AIMemorySettings> _memorySettingsService;
    private readonly AppDataSettingsService<InteractionDocumentSettings> _interactionDocumentSettingsService;
    private readonly AppDataSettingsService<AIDataSourceSettings> _aiDataSourceSettingsService;
    private readonly AppDataSettingsService<McpServerOptions> _mcpServerSettingsService;
    private readonly AppDataSettingsService<MemoryMetadata> _chatInteractionMemorySettingsService;
    private readonly AppDataSettingsService<CopilotSettings> _copilotSettingsService;
    private readonly AppDataSettingsService<PaginationSettings> _paginationSettingsService;
    private readonly AppDataSettingsService<AIChatAdminWidgetSettings> _adminWidgetSettingsService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIProfileManager _profileManager;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ISpeechVoiceResolver _speechVoiceResolver;

    public SettingsController(
        AppDataSettingsService<GeneralAISettings> settingsService,
        AppDataSettingsService<ChatInteractionSettings> chatInteractionSettingsService,
        AppDataSettingsService<DefaultOrchestratorSettings> defaultOrchestratorSettingsService,
        AppDataSettingsService<DefaultAIDeploymentSettings> deploymentDefaultsService,
        AppDataSettingsService<AIMemorySettings> memorySettingsService,
        AppDataSettingsService<InteractionDocumentSettings> interactionDocumentSettingsService,
        AppDataSettingsService<AIDataSourceSettings> aiDataSourceSettingsService,
        AppDataSettingsService<McpServerOptions> mcpServerSettingsService,
        AppDataSettingsService<MemoryMetadata> chatInteractionMemorySettingsService,
        AppDataSettingsService<CopilotSettings> copilotSettingsService,
        AppDataSettingsService<PaginationSettings> paginationSettingsService,
        AppDataSettingsService<AIChatAdminWidgetSettings> adminWidgetSettingsService,
        IAIDeploymentManager deploymentManager,
        IAIProfileManager profileManager,
        ISearchIndexProfileStore indexProfileStore,
        IDataProtectionProvider dataProtectionProvider,
        ISpeechVoiceResolver speechVoiceResolver)
    {
        _settingsService = settingsService;
        _chatInteractionSettingsService = chatInteractionSettingsService;
        _defaultOrchestratorSettingsService = defaultOrchestratorSettingsService;
        _deploymentDefaultsService = deploymentDefaultsService;
        _memorySettingsService = memorySettingsService;
        _interactionDocumentSettingsService = interactionDocumentSettingsService;
        _aiDataSourceSettingsService = aiDataSourceSettingsService;
        _mcpServerSettingsService = mcpServerSettingsService;
        _chatInteractionMemorySettingsService = chatInteractionMemorySettingsService;
        _copilotSettingsService = copilotSettingsService;
        _paginationSettingsService = paginationSettingsService;
        _adminWidgetSettingsService = adminWidgetSettingsService;
        _deploymentManager = deploymentManager;
        _profileManager = profileManager;
        _indexProfileStore = indexProfileStore;
        _dataProtectionProvider = dataProtectionProvider;
        _speechVoiceResolver = speechVoiceResolver;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _settingsService.GetAsync();
        var chatInteractionSettings = await _chatInteractionSettingsService.GetAsync();
        var defaultOrchestratorSettings = await _defaultOrchestratorSettingsService.GetAsync();
        var deploymentDefaults = await _deploymentDefaultsService.GetAsync();
        var memorySettings = await _memorySettingsService.GetAsync();
        var documentSettings = await _interactionDocumentSettingsService.GetAsync();
        var dataSourceSettings = await _aiDataSourceSettingsService.GetAsync();
        var mcpServerSettings = await _mcpServerSettingsService.GetAsync();
        var chatInteractionMemorySettings = await _chatInteractionMemorySettingsService.GetAsync();
        var copilotSettings = await _copilotSettingsService.GetAsync();
        var paginationSettings = await _paginationSettingsService.GetAsync();
        var adminWidgetSettings = await _adminWidgetSettingsService.GetAsync();

        var model = new SettingsViewModel
        {
            EnableAIUsageTracking = settings.EnableAIUsageTracking,
            EnablePreemptiveMemoryRetrieval = settings.EnablePreemptiveMemoryRetrieval,
            MaximumIterationsPerRequest = settings.MaximumIterationsPerRequest,
            EnableDistributedCaching = settings.EnableDistributedCaching,
            EnableOpenTelemetry = settings.EnableOpenTelemetry,
            ChatInteractionChatMode = chatInteractionSettings.ChatMode,
            DefaultOrchestratorEnablePreemptiveRag = defaultOrchestratorSettings.EnablePreemptiveRag,
            MemoryIndexProfileName = memorySettings.IndexProfileName,
            MemoryTopN = memorySettings.TopN,
            EnableUserMemoryByDefault = chatInteractionMemorySettings.EnableUserMemory ?? true,

            DefaultChatDeploymentName = deploymentDefaults.DefaultChatDeploymentName,
            DefaultUtilityDeploymentName = deploymentDefaults.DefaultUtilityDeploymentName,
            DefaultEmbeddingDeploymentName = deploymentDefaults.DefaultEmbeddingDeploymentName,
            DefaultImageDeploymentName = deploymentDefaults.DefaultImageDeploymentName,
            DefaultSpeechToTextDeploymentName = deploymentDefaults.DefaultSpeechToTextDeploymentName,
            DefaultTextToSpeechDeploymentName = deploymentDefaults.DefaultTextToSpeechDeploymentName,
            DefaultTextToSpeechVoiceId = deploymentDefaults.DefaultTextToSpeechVoiceId,

            DocumentIndexProfileName = documentSettings.IndexProfileName,
            DocumentTopN = documentSettings.TopN,
            DataSourceDefaultStrictness = dataSourceSettings.DefaultStrictness,
            DataSourceDefaultTopNDocuments = dataSourceSettings.DefaultTopNDocuments,
            McpServerAuthenticationType = mcpServerSettings.AuthenticationType,
            McpServerApiKey = mcpServerSettings.ApiKey,
            McpServerRequireAccessPermission = mcpServerSettings.RequireAccessPermission,

            CopilotAuthenticationType = copilotSettings.AuthenticationType,
            CopilotClientId = copilotSettings.ClientId,
            CopilotHasSecret = !string.IsNullOrWhiteSpace(copilotSettings.ProtectedClientSecret),
            CopilotProviderType = copilotSettings.ProviderType,
            CopilotBaseUrl = copilotSettings.BaseUrl,
            CopilotHasApiKey = !string.IsNullOrWhiteSpace(copilotSettings.ProtectedApiKey),
            CopilotWireApi = copilotSettings.WireApi ?? "completions",
            CopilotDefaultModel = copilotSettings.DefaultModel,
            CopilotAzureApiVersion = copilotSettings.AzureApiVersion,
            CopilotCallbackUrl = Url.Action("OAuthCallback", "CopilotAuth", new { area = "AIChat" }, Request.Scheme),
            AdminPageSize = paginationSettings.AdminPageSize,
            AdminWidgetProfileId = adminWidgetSettings.ProfileId,
            AdminWidgetPrimaryColor = string.IsNullOrWhiteSpace(adminWidgetSettings.PrimaryColor)
                ? AIChatAdminWidgetSettings.DefaultSecondaryColor
                : adminWidgetSettings.PrimaryColor,
        };

        await NormalizeDeploymentSelectorsAsync(model);
        await PopulateDeploymentDropdownsAsync(model);
        await PopulateAdminWidgetProfilesAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SettingsViewModel model)
    {
        if (model.MaximumIterationsPerRequest < 1)
        {
            ModelState.AddModelError(nameof(model.MaximumIterationsPerRequest), "Must be at least 1.");
        }

        if (model.DocumentTopN < 1)
        {
            ModelState.AddModelError(nameof(model.DocumentTopN), "Must be at least 1.");
        }

        if (model.MemoryTopN < 1 || model.MemoryTopN > 20)
        {
            ModelState.AddModelError(nameof(model.MemoryTopN), "Must be between 1 and 20.");
        }

        if (model.DataSourceDefaultStrictness < AIDataSourceSettings.MinStrictness || model.DataSourceDefaultStrictness > AIDataSourceSettings.MaxStrictness)
        {
            ModelState.AddModelError(nameof(model.DataSourceDefaultStrictness), $"Must be between {AIDataSourceSettings.MinStrictness} and {AIDataSourceSettings.MaxStrictness}.");
        }

        if (model.DataSourceDefaultTopNDocuments < AIDataSourceSettings.MinTopNDocuments || model.DataSourceDefaultTopNDocuments > AIDataSourceSettings.MaxTopNDocuments)
        {
            ModelState.AddModelError(nameof(model.DataSourceDefaultTopNDocuments), $"Must be between {AIDataSourceSettings.MinTopNDocuments} and {AIDataSourceSettings.MaxTopNDocuments}.");
        }

        if (model.McpServerAuthenticationType == McpServerAuthenticationType.ApiKey &&
            string.IsNullOrWhiteSpace(model.McpServerApiKey))
        {
            ModelState.AddModelError(nameof(model.McpServerApiKey), "API key is required when the MCP server uses API key authentication.");
        }

        if (model.AdminPageSize < 1 || model.AdminPageSize > 200)
        {
            ModelState.AddModelError(nameof(model.AdminPageSize), "Page size must be between 1 and 200.");
        }

        if (!string.IsNullOrWhiteSpace(model.AdminWidgetPrimaryColor) &&
            !Regex.IsMatch(
                model.AdminWidgetPrimaryColor,
                "^#(?:[0-9a-fA-F]{3}){1,2}$",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)))
        {
            ModelState.AddModelError(nameof(model.AdminWidgetPrimaryColor), "Color must be a valid hex value such as #6c757d.");
        }

        if (!string.IsNullOrWhiteSpace(model.AdminWidgetProfileId))
        {
            var profile = await _profileManager.FindByIdAsync(model.AdminWidgetProfileId);

            if (profile is null || profile.Type != AIProfileType.Chat)
            {
                ModelState.AddModelError(nameof(model.AdminWidgetProfileId), "Invalid admin widget profile.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.MemoryIndexProfileName))
        {
            var indexProfile = await _indexProfileStore.FindByNameAsync(model.MemoryIndexProfileName);

            if (indexProfile is null || !string.Equals(indexProfile.Type, MemoryIndexProfileType, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.MemoryIndexProfileName), "Invalid memory index profile.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateDeploymentDropdownsAsync(model);
            await PopulateAdminWidgetProfilesAsync(model);

            return View(nameof(Index), model);
        }

        // Save general AI settings.
        var settings = await _settingsService.GetAsync();

        settings.EnablePreemptiveMemoryRetrieval = model.EnablePreemptiveMemoryRetrieval;
        settings.EnableAIUsageTracking = model.EnableAIUsageTracking;
        settings.MaximumIterationsPerRequest = model.MaximumIterationsPerRequest;
        settings.EnableDistributedCaching = model.EnableDistributedCaching;
        settings.EnableOpenTelemetry = model.EnableOpenTelemetry;

        await _settingsService.SaveAsync(settings);

        var chatInteractionSettings = await _chatInteractionSettingsService.GetAsync();
        chatInteractionSettings.ChatMode = model.ChatInteractionChatMode;

        await _chatInteractionSettingsService.SaveAsync(chatInteractionSettings);

        await _defaultOrchestratorSettingsService.SaveAsync(new DefaultOrchestratorSettings
        {
            EnablePreemptiveRag = model.DefaultOrchestratorEnablePreemptiveRag,
        });

        // Save default deployment settings.
        var deploymentDefaults = new DefaultAIDeploymentSettings
        {
            DefaultChatDeploymentName = model.DefaultChatDeploymentName,
            DefaultUtilityDeploymentName = model.DefaultUtilityDeploymentName,
            DefaultEmbeddingDeploymentName = model.DefaultEmbeddingDeploymentName,
            DefaultImageDeploymentName = model.DefaultImageDeploymentName,
            DefaultSpeechToTextDeploymentName = model.DefaultSpeechToTextDeploymentName,
            DefaultTextToSpeechDeploymentName = model.DefaultTextToSpeechDeploymentName,
            DefaultTextToSpeechVoiceId = model.DefaultTextToSpeechVoiceId?.Trim(),
        };

        await _deploymentDefaultsService.SaveAsync(deploymentDefaults);

        await _memorySettingsService.SaveAsync(new AIMemorySettings
        {
            IndexProfileName = string.IsNullOrWhiteSpace(model.MemoryIndexProfileName)
                ? null
                : model.MemoryIndexProfileName.Trim(),
            TopN = model.MemoryTopN,
        });

        await _interactionDocumentSettingsService.SaveAsync(new InteractionDocumentSettings
        {
            IndexProfileName = model.DocumentIndexProfileName?.Trim(),
            TopN = model.DocumentTopN,
        });

        await _aiDataSourceSettingsService.SaveAsync(new AIDataSourceSettings
        {
            DefaultStrictness = model.DataSourceDefaultStrictness,
            DefaultTopNDocuments = model.DataSourceDefaultTopNDocuments,
        });

        await _mcpServerSettingsService.SaveAsync(new McpServerOptions
        {
            AuthenticationType = model.McpServerAuthenticationType,
            ApiKey = model.McpServerApiKey?.Trim(),
            RequireAccessPermission = model.McpServerRequireAccessPermission,
        });

        await _chatInteractionMemorySettingsService.SaveAsync(new MemoryMetadata
        {
            EnableUserMemory = model.EnableUserMemoryByDefault,
        });

        // Save Copilot settings.
        var existingCopilot = await _copilotSettingsService.GetAsync();
        var protector = _dataProtectionProvider.CreateProtector(CopilotProtectorPurpose);

        var copilotSettings = new CopilotSettings
        {
            AuthenticationType = model.CopilotAuthenticationType,
            ClientId = model.CopilotClientId?.Trim(),
            ProtectedClientSecret = existingCopilot.ProtectedClientSecret,
            ProviderType = model.CopilotProviderType,
            BaseUrl = model.CopilotBaseUrl?.Trim(),
            ProtectedApiKey = existingCopilot.ProtectedApiKey,
            WireApi = model.CopilotWireApi,
            DefaultModel = model.CopilotDefaultModel?.Trim(),
            AzureApiVersion = model.CopilotAzureApiVersion?.Trim(),
        };

        if (!string.IsNullOrWhiteSpace(model.CopilotClientSecret))
        {
            copilotSettings.ProtectedClientSecret = protector.Protect(model.CopilotClientSecret.Trim());
        }

        if (!string.IsNullOrWhiteSpace(model.CopilotApiKey))
        {
            copilotSettings.ProtectedApiKey = protector.Protect(model.CopilotApiKey.Trim());
        }

        await _copilotSettingsService.SaveAsync(copilotSettings);

        // Save pagination settings.
        await _paginationSettingsService.SaveAsync(new PaginationSettings
        {
            AdminPageSize = model.AdminPageSize,
        });

        await _adminWidgetSettingsService.SaveAsync(new AIChatAdminWidgetSettings
        {
            ProfileId = string.IsNullOrWhiteSpace(model.AdminWidgetProfileId) ? null : model.AdminWidgetProfileId.Trim(),
            PrimaryColor = string.IsNullOrWhiteSpace(model.AdminWidgetPrimaryColor)
                ? AIChatAdminWidgetSettings.DefaultSecondaryColor
                : model.AdminWidgetPrimaryColor.Trim(),
        });

        TempData["SuccessMessage"] = "Settings saved successfully.";

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDeploymentDropdownsAsync(SettingsViewModel model)
    {
        model.ChatDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.Chat));

        model.UtilityDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.Utility));

        model.EmbeddingDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.Embedding));

        model.ImageDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.Image));

        model.SpeechToTextDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.SpeechToText));

        model.TextToSpeechDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.TextToSpeech));

        model.ChatInteractionModes =
        [
            new SelectListItem("Text input", nameof(ChatMode.TextInput)),
            new SelectListItem("Audio input", nameof(ChatMode.AudioInput)),
            new SelectListItem("Conversation", nameof(ChatMode.Conversation)),
        ];

        model.DocumentIndexProfiles = (await _indexProfileStore.GetByTypeAsync(IndexProfileTypes.AIDocuments))
            .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.Name));

        model.MemoryIndexProfiles = (await _indexProfileStore.GetByTypeAsync(MemoryIndexProfileType))
            .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.Name));
    }

    private async Task PopulateAdminWidgetProfilesAsync(SettingsViewModel model)
    {
        model.AdminWidgetProfiles = (await _profileManager.GetAsync(AIProfileType.Chat))
            .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SelectListItem(
                profile.DisplayText ?? profile.Name,
                profile.ItemId,
                profile.ItemId == model.AdminWidgetProfileId));
    }

    private static IEnumerable<SelectListItem> BuildGroupedDeploymentItems(IEnumerable<AIDeployment> deployments)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(d => d.ConnectionNameAlias ?? d.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d =>
            {
                SelectListGroup group = null;
                var groupKey = d.ConnectionNameAlias ?? d.ConnectionName;

                if (!string.IsNullOrEmpty(groupKey) && !groups.TryGetValue(groupKey, out group))
                {
                    group = new SelectListGroup { Name = groupKey };

                    groups[groupKey] = group;
                }

                var label = string.Equals(d.Name, d.ModelName, StringComparison.OrdinalIgnoreCase)
                ? d.Name
                : $"{d.Name} ({d.ModelName})";

                return new SelectListItem(label, d.Name) { Group = group };

            });
    }

    private async Task NormalizeDeploymentSelectorsAsync(SettingsViewModel model)
    {
        model.DefaultChatDeploymentName = await NormalizeDeploymentSelectorAsync(model.DefaultChatDeploymentName);
        model.DefaultUtilityDeploymentName = await NormalizeDeploymentSelectorAsync(model.DefaultUtilityDeploymentName);
        model.DefaultEmbeddingDeploymentName = await NormalizeDeploymentSelectorAsync(model.DefaultEmbeddingDeploymentName);
        model.DefaultImageDeploymentName = await NormalizeDeploymentSelectorAsync(model.DefaultImageDeploymentName);
        model.DefaultSpeechToTextDeploymentName = await NormalizeDeploymentSelectorAsync(model.DefaultSpeechToTextDeploymentName);
        model.DefaultTextToSpeechDeploymentName = await NormalizeDeploymentSelectorAsync(model.DefaultTextToSpeechDeploymentName);
    }

    private async Task<string> NormalizeDeploymentSelectorAsync(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return selector;
        }

        var deployment = await _deploymentManager.FindByIdAsync(selector);

        return deployment?.Name ?? selector;
    }

    [HttpGet]
    public async Task<IActionResult> GetVoices(string deploymentName)
    {
        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            return Json(new { voices = Array.Empty<object>() });
        }

        var deployment = await _deploymentManager.FindByNameAsync(deploymentName);

        if (deployment is null)
        {
            return Json(new { voices = Array.Empty<object>() });
        }

        var voices = (await _speechVoiceResolver.GetSpeechVoicesAsync(deployment))
            .OrderBy(voice => voice.Language, StringComparer.OrdinalIgnoreCase)
            .ThenBy(voice => voice.Name, StringComparer.OrdinalIgnoreCase)
            .Select(voice => new
            {
                voice.Id,
                voice.Name,
                voice.Language,
                LanguageDisplayName = GetCultureDisplayName(voice.Language),
                Gender = voice.Gender.ToString(),
            });

        return Json(new { voices });
    }

    private static string GetCultureDisplayName(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "Unknown";
        }

        try
        {
            return CultureInfo.GetCultureInfo(language).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return language;
        }
    }
}
