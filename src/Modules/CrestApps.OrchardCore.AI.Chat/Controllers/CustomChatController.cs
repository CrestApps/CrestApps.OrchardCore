using System.Security.Claims;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

[Admin]
public sealed class CustomChatController : Controller
{
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIChatSession> _sessionDisplayManager;
    private readonly AIOptions _aiOptions;
    private readonly AIProviderOptions _connectionOptions;
    private readonly DefaultAIOptions _defaultAIOptions;
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CustomChatController(
        IAIChatSessionManager sessionManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIChatSession> sessionDisplayManager,
        IOptions<AIOptions> aiOptions,
        IOptions<AIProviderOptions> connectionOptions,
        IOptions<DefaultAIOptions> defaultAIOptions,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<CustomChatController> htmlLocalizer,
        IStringLocalizer<CustomChatController> stringLocalizer
        )
    {
        _sessionManager = sessionManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _sessionDisplayManager = sessionDisplayManager;
        _aiOptions = aiOptions.Value;
        _connectionOptions = connectionOptions.Value;
        _defaultAIOptions = defaultAIOptions.Value;
        _toolDefinitions = toolDefinitions.Value;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/custom-chat", "CustomChatIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var userId = CurrentUserId();

        // Get all custom chat instances for the current user
        var sessions = await _sessionManager.PageAsync(1, 100, new AIChatSessionQueryContext
        {
            UserId = userId,
        });

        // Filter only custom instances
        var customInstances = sessions.Sessions
            .Where(s => s.As<AIChatInstanceMetadata>()?.IsCustomInstance == true)
            .ToList();

        // Create default configuration for new instance
        var defaultConfig = await BuildDefaultConfigurationAsync();

        var model = new ManageCustomChatInstancesViewModel
        {
            Instances = customInstances,
            Configuration = defaultConfig,
            IsNew = true
        };

        return View(model);
    }

    [Admin("ai/custom-chat/{sessionId}", "CustomChatEdit")]
    public async Task<IActionResult> Edit(string sessionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var session = await _sessionManager.FindAsync(sessionId);

        if (session == null)
        {
            return NotFound();
        }

        var userId = CurrentUserId();

        if (session.UserId != userId)
        {
            return Forbid();
        }

        var metadata = session.As<AIChatInstanceMetadata>();

        if (metadata?.IsCustomInstance != true)
        {
            return NotFound();
        }

        // Get all custom chat instances for the sidebar
        var sessions = await _sessionManager.PageAsync(1, 100, new AIChatSessionQueryContext
        {
            UserId = userId,
        });

        var customInstances = sessions.Sessions
            .Where(s => s.As<AIChatInstanceMetadata>()?.IsCustomInstance == true)
            .ToList();

        var configuration = await BuildConfigurationViewModelAsync(session, metadata);

        // Build chat content using the display manager
        var chatContent = await _sessionDisplayManager.BuildEditorAsync(session, _updateModelAccessor.ModelUpdater, isNew: false);

        var model = new ManageCustomChatInstancesViewModel
        {
            CurrentSession = session,
            Configuration = configuration,
            ChatContent = chatContent,
            Instances = customInstances,
            IsNew = false
        };

        return View("Index", model);
    }

    [HttpPost]
    [Admin("ai/custom-chat/save", "CustomChatSave")]
    public async Task<IActionResult> Save(CustomChatInstanceViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        await TryUpdateModelAsync(model, string.Empty);

        if (!ModelState.IsValid)
        {
            await _notifier.ErrorAsync(H["Please fix the validation errors."]);
            return RedirectToAction(nameof(Index));
        }

        var userId = CurrentUserId();
        AIChatSession session;

        if (string.IsNullOrEmpty(model.SessionId))
        {
            // Create new instance
            session = new AIChatSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                UserId = userId,
                Title = model.Title,
                CreatedUtc = DateTime.UtcNow,
                ProfileId = "custom-" + Guid.NewGuid().ToString("N") // Placeholder profile ID
            };
        }
        else
        {
            session = await _sessionManager.FindAsync(model.SessionId);

            if (session == null || session.UserId != userId)
            {
                return NotFound();
            }

            session.Title = model.Title;
        }

        // Store configuration as metadata
        var metadata = new AIChatInstanceMetadata
        {
            IsCustomInstance = true,
            ConnectionName = model.ConnectionName,
            DeploymentId = model.DeploymentId,
            SystemMessage = model.SystemMessage,
            MaxTokens = model.MaxTokens,
            Temperature = model.Temperature,
            TopP = model.TopP,
            FrequencyPenalty = model.FrequencyPenalty,
            PresencePenalty = model.PresencePenalty,
            PastMessagesCount = model.PastMessagesCount,
            UseCaching = model.UseCaching,
            ProviderName = model.ProviderName,
            Source = GetSourceFromProvider(model.ProviderName),
            ToolNames = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId).ToArray() ?? []
        };

        session.Put(metadata);

        await _sessionManager.SaveAsync(session);

        await _notifier.SuccessAsync(H["Chat instance saved successfully."]);

        return RedirectToAction(nameof(Edit), new { sessionId = session.SessionId });
    }

    [HttpPost]
    [Admin("ai/custom-chat/delete/{sessionId}", "CustomChatDelete")]
    public async Task<IActionResult> Delete(string sessionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var session = await _sessionManager.FindAsync(sessionId);

        if (session == null)
        {
            return NotFound();
        }

        var userId = CurrentUserId();

        if (session.UserId != userId)
        {
            return Forbid();
        }

        var metadata = session.As<AIChatInstanceMetadata>();

        if (metadata?.IsCustomInstance != true)
        {
            return NotFound();
        }

        if (await _sessionManager.DeleteAsync(sessionId))
        {
            await _notifier.SuccessAsync(H["Chat instance deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to delete the chat instance."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Admin("ai/custom-chat/api/deployments", "CustomChatGetDeployments")]
    public IActionResult GetDeployments(string providerName, string connection)
    {
        if (!_connectionOptions.Providers.TryGetValue(providerName, out var provider))
        {
            return NotFound();
        }

        if (!provider.Connections.TryGetValue(connection, out var connectionConfig))
        {
            return NotFound();
        }

        if (!connectionConfig.TryGetValue("Deployments", out var deploymentsObj))
        {
            return Json(Array.Empty<object>());
        }

        var deployments = deploymentsObj as IDictionary<string, object>;

        if (deployments == null)
        {
            return Json(Array.Empty<object>());
        }

        var result = deployments.Select(d => new
        {
            id = d.Key,
            name = d.Value.ToString()
        });

        return Json(result);
    }

    private async Task<CustomChatInstanceViewModel> BuildDefaultConfigurationAsync()
    {
        var model = new CustomChatInstanceViewModel
        {
            MaxTokens = _defaultAIOptions.MaxOutputTokens,
            Temperature = _defaultAIOptions.Temperature,
            TopP = _defaultAIOptions.TopP,
            FrequencyPenalty = _defaultAIOptions.FrequencyPenalty,
            PresencePenalty = _defaultAIOptions.PresencePenalty,
            PastMessagesCount = _defaultAIOptions.PastMessagesCount,
            UseCaching = true,
            AllowCaching = _defaultAIOptions.EnableDistributedCaching,
            IsNew = true
        };

        // Set default provider
        model.ProviderName = _connectionOptions.Providers.Keys.FirstOrDefault();

        // Populate connections
        if (!string.IsNullOrEmpty(model.ProviderName) && _connectionOptions.Providers.TryGetValue(model.ProviderName, out var provider))
        {
            model.ConnectionNames = provider.Connections
                .Select(x => new SelectListItem(
                    x.Value.TryGetValue("ConnectionNameAlias", out var alias) ? alias.ToString() : x.Key,
                    x.Key))
                .ToList();

            // Set default connection if only one available
            if (provider.Connections.Count == 1)
            {
                model.ConnectionName = provider.Connections.First().Key;

                // Populate deployments for the default connection
                if (provider.Connections.First().Value.TryGetValue("Deployments", out var deploymentsObj) && deploymentsObj is IDictionary<string, object> deployments)
                {
                    model.Deployments = deployments.Select(d => new SelectListItem(d.Value.ToString(), d.Key)).ToList();
                }
            }
        }
        else
        {
            model.ConnectionNames = [];
            model.Deployments = [];
        }

        // Populate tools (none selected by default)
        if (_toolDefinitions.Tools.Count > 0)
        {
            model.Tools = _toolDefinitions.Tools
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"].Value)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = false, // No tools selected by default
                }).OrderBy(entry => entry.DisplayText).ToArray());
        }

        return await Task.FromResult(model);
    }

    private async Task<CustomChatInstanceViewModel> BuildConfigurationViewModelAsync(AIChatSession session, AIChatInstanceMetadata metadata)
    {
        var model = new CustomChatInstanceViewModel
        {
            SessionId = session.SessionId,
            Title = session.Title,
            ConnectionName = metadata.ConnectionName,
            DeploymentId = metadata.DeploymentId,
            SystemMessage = metadata.SystemMessage,
            MaxTokens = metadata.MaxTokens ?? _defaultAIOptions.MaxOutputTokens,
            Temperature = metadata.Temperature ?? _defaultAIOptions.Temperature,
            TopP = metadata.TopP ?? _defaultAIOptions.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? _defaultAIOptions.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? _defaultAIOptions.PresencePenalty,
            PastMessagesCount = metadata.PastMessagesCount ?? _defaultAIOptions.PastMessagesCount,
            UseCaching = metadata.UseCaching,
            ProviderName = metadata.ProviderName,
            AllowCaching = _defaultAIOptions.EnableDistributedCaching,
            IsNew = false
        };

        // Set default provider if not set
        if (string.IsNullOrEmpty(model.ProviderName))
        {
            model.ProviderName = _connectionOptions.Providers.Keys.FirstOrDefault();
        }

        // Populate connections
        if (!string.IsNullOrEmpty(model.ProviderName) && _connectionOptions.Providers.TryGetValue(model.ProviderName, out var provider))
        {
            model.ConnectionNames = provider.Connections
                .Select(x => new SelectListItem(
                    x.Value.TryGetValue("ConnectionNameAlias", out var alias) ? alias.ToString() : x.Key,
                    x.Key))
                .ToList();

            // Set default connection if not set
            if (string.IsNullOrEmpty(model.ConnectionName) && provider.Connections.Count == 1)
            {
                model.ConnectionName = provider.Connections.First().Key;
            }

            // Populate deployments if connection is set
            if (!string.IsNullOrEmpty(model.ConnectionName) && provider.Connections.TryGetValue(model.ConnectionName, out var connectionConfig))
            {
                if (connectionConfig.TryGetValue("Deployments", out var deploymentsObj) && deploymentsObj is IDictionary<string, object> deployments)
                {
                    model.Deployments = deployments.Select(d => new SelectListItem(d.Value.ToString(), d.Key)).ToList();
                }
            }
        }
        else
        {
            model.ConnectionNames = [];
            model.Deployments = [];
        }

        // Populate tools
        if (_toolDefinitions.Tools.Count > 0)
        {
            model.Tools = _toolDefinitions.Tools
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"].Value)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = metadata.ToolNames?.Contains(entry.Key) ?? false,
                }).OrderBy(entry => entry.DisplayText).ToArray());
        }

        return model;
    }

    private string GetSourceFromProvider(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            // Get the first available profile source
            return _aiOptions.ProfileSources.Keys.FirstOrDefault();
        }

        // Try to find a matching profile source for this provider
        var matchingSource = _aiOptions.ProfileSources.FirstOrDefault(ps => 
            ps.Value.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        return matchingSource.Key ?? providerName;
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
