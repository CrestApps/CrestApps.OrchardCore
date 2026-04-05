using CrestApps.AI.Deployments;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Mvc.Web.Areas.Admin.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Mvc.Web.Areas.AIChat.Models;
using CrestApps.Mvc.Web.Areas.ChatInteractions.Models;
using CrestApps.Mvc.Web.Models;
using CrestApps.Mvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class SettingsController : Controller
{
    private const string CopilotProtectorPurpose = "CrestApps.Mvc.Web.CopilotSettings";
    private const string MemoryIndexProfileType = "AIMemory";

    private readonly AppDataSettingsService<GeneralAISettings> _settingsService;
    private readonly AppDataSettingsService<DefaultOrchestratorSettings> _defaultOrchestratorSettingsService;
    private readonly AppDataSettingsService<DefaultAIDeploymentSettings> _deploymentDefaultsService;
    private readonly AppDataSettingsService<AIMemorySettings> _memorySettingsService;
    private readonly AppDataSettingsService<InteractionDocumentSettings> _interactionDocumentSettingsService;
    private readonly AppDataSettingsService<AIDataSourceSettings> _aiDataSourceSettingsService;
    private readonly AppDataSettingsService<McpServerOptions> _mcpServerSettingsService;
    private readonly AppDataSettingsService<ChatInteractionSettings> _chatInteractionSettingsService;
    private readonly AppDataSettingsService<MemoryMetadata> _chatInteractionMemorySettingsService;
    private readonly AppDataSettingsService<CopilotSettings> _copilotSettingsService;
    private readonly AppDataSettingsService<PaginationSettings> _paginationSettingsService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public SettingsController(
        AppDataSettingsService<GeneralAISettings> settingsService,
        AppDataSettingsService<DefaultOrchestratorSettings> defaultOrchestratorSettingsService,
        AppDataSettingsService<DefaultAIDeploymentSettings> deploymentDefaultsService,
        AppDataSettingsService<AIMemorySettings> memorySettingsService,
        AppDataSettingsService<InteractionDocumentSettings> interactionDocumentSettingsService,
        AppDataSettingsService<AIDataSourceSettings> aiDataSourceSettingsService,
        AppDataSettingsService<McpServerOptions> mcpServerSettingsService,
        AppDataSettingsService<ChatInteractionSettings> chatInteractionSettingsService,
        AppDataSettingsService<MemoryMetadata> chatInteractionMemorySettingsService,
        AppDataSettingsService<CopilotSettings> copilotSettingsService,
        AppDataSettingsService<PaginationSettings> paginationSettingsService,
        IAIDeploymentManager deploymentManager,
        ISearchIndexProfileStore indexProfileStore,
        IDataProtectionProvider dataProtectionProvider)
    {
        _settingsService = settingsService;
        _defaultOrchestratorSettingsService = defaultOrchestratorSettingsService;
        _deploymentDefaultsService = deploymentDefaultsService;
        _memorySettingsService = memorySettingsService;
        _interactionDocumentSettingsService = interactionDocumentSettingsService;
        _aiDataSourceSettingsService = aiDataSourceSettingsService;
        _mcpServerSettingsService = mcpServerSettingsService;
        _chatInteractionSettingsService = chatInteractionSettingsService;
        _chatInteractionMemorySettingsService = chatInteractionMemorySettingsService;
        _copilotSettingsService = copilotSettingsService;
        _paginationSettingsService = paginationSettingsService;
        _deploymentManager = deploymentManager;
        _indexProfileStore = indexProfileStore;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _settingsService.GetAsync();
        var defaultOrchestratorSettings = await _defaultOrchestratorSettingsService.GetAsync();
        var deploymentDefaults = await _deploymentDefaultsService.GetAsync();
        var memorySettings = await _memorySettingsService.GetAsync();
        var documentSettings = await _interactionDocumentSettingsService.GetAsync();
        var dataSourceSettings = await _aiDataSourceSettingsService.GetAsync();
        var mcpServerSettings = await _mcpServerSettingsService.GetAsync();
        var chatInteractionMemorySettings = await _chatInteractionMemorySettingsService.GetAsync();
        var copilotSettings = await _copilotSettingsService.GetAsync();
        var paginationSettings = await _paginationSettingsService.GetAsync();

        var model = new SettingsViewModel
        {
            EnablePreemptiveMemoryRetrieval = settings.EnablePreemptiveMemoryRetrieval,
            MaximumIterationsPerRequest = settings.MaximumIterationsPerRequest,
            EnableDistributedCaching = settings.EnableDistributedCaching,
            EnableOpenTelemetry = settings.EnableOpenTelemetry,
            DefaultOrchestratorEnablePreemptiveRag = defaultOrchestratorSettings.EnablePreemptiveRag,
            MemoryIndexProfileName = memorySettings.IndexProfileName,
            MemoryTopN = memorySettings.TopN,

            ChatInteractionEnableUserMemory = chatInteractionMemorySettings.EnableUserMemory ?? true,

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
        };

        await NormalizeDeploymentSelectorsAsync(model);
        await PopulateDeploymentDropdownsAsync(model);

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

            return View(nameof(Index), model);
        }

        // Save general AI settings.
        var settings = await _settingsService.GetAsync();

        settings.EnablePreemptiveMemoryRetrieval = model.EnablePreemptiveMemoryRetrieval;
        settings.MaximumIterationsPerRequest = model.MaximumIterationsPerRequest;
        settings.EnableDistributedCaching = model.EnableDistributedCaching;
        settings.EnableOpenTelemetry = model.EnableOpenTelemetry;

        await _settingsService.SaveAsync(settings);

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

        await _chatInteractionSettingsService.SaveAsync(new ChatInteractionSettings
        {
            EnableUserMemory = model.ChatInteractionEnableUserMemory,
        });

        await _chatInteractionMemorySettingsService.SaveAsync(new MemoryMetadata
        {
            EnableUserMemory = model.ChatInteractionEnableUserMemory,
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

        model.DocumentIndexProfiles = (await _indexProfileStore.GetByTypeAsync(IndexProfileTypes.AIDocuments))
            .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.Name));

        model.MemoryIndexProfiles = (await _indexProfileStore.GetByTypeAsync(MemoryIndexProfileType))
            .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.Name));
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
}
