using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

[Admin]
public sealed class CustomChatController : Controller
{
    private readonly ICustomChatInstanceManager _instanceManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly AIOptions _aiOptions;
    private readonly AIProviderOptions _providerOptions;
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CustomChatController(
        ICustomChatInstanceManager instanceManager,
        IAuthorizationService authorizationService,
        IAIDeploymentManager deploymentManager,
        IOptions<AIOptions> aiOptions,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        INotifier notifier,
        IHtmlLocalizer<CustomChatController> htmlLocalizer,
        IStringLocalizer<CustomChatController> stringLocalizer)
    {
        _instanceManager = instanceManager;
        _authorizationService = authorizationService;
        _deploymentManager = deploymentManager;
        _aiOptions = aiOptions.Value;
        _providerOptions = providerOptions.Value;
        _toolDefinitions = toolDefinitions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/custom-chat", "CustomChatIndex")]
    public async Task<IActionResult> Index(string instanceId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        var instances = (await _instanceManager.GetAllAsync()).ToList();

        AICustomChatInstance activeInstance = null;

        if (!string.IsNullOrEmpty(instanceId))
        {
            activeInstance = await _instanceManager.FindByIdAsync(instanceId);
        }

        activeInstance ??= instances.FirstOrDefault();

        var model = new CustomChatIndexViewModel
        {
            Instances = instances,
            ActiveInstanceId = activeInstance?.InstanceId,
        };

        if (activeInstance != null)
        {
            ViewData["ActiveInstance"] = activeInstance;
        }

        return View(model);
    }

    [Admin("ai/custom-chat/create", "CustomChatCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        var instance = await _instanceManager.NewAsync();
        var model = await PopulateViewModelAsync(instance, isNew: true);

        return View(model);
    }

    [HttpPost]
    [Admin("ai/custom-chat/create", "CustomChatCreate")]
    public async Task<IActionResult> Create(CustomChatInstanceViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        var instance = await _instanceManager.NewAsync();
        MapToInstance(model, instance);

        await _instanceManager.SaveAsync(instance);

        await _notifier.SuccessAsync(H["Custom chat instance created successfully."]);

        return RedirectToAction(nameof(Index), new { instanceId = instance.InstanceId });
    }

    [Admin("ai/custom-chat/edit/{instanceId}", "CustomChatEdit")]
    public async Task<IActionResult> Edit(string instanceId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        var instance = await _instanceManager.FindByIdAsync(instanceId);

        if (instance == null)
        {
            return NotFound();
        }

        var model = await PopulateViewModelAsync(instance, isNew: false);

        return View(model);
    }

    [HttpPost]
    [Admin("ai/custom-chat/edit/{instanceId}", "CustomChatEdit")]
    public async Task<IActionResult> Edit(string instanceId, CustomChatInstanceViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        var instance = await _instanceManager.FindByIdAsync(instanceId);

        if (instance == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        MapToInstance(model, instance);

        await _instanceManager.SaveAsync(instance);

        await _notifier.SuccessAsync(H["Custom chat instance updated successfully."]);

        return RedirectToAction(nameof(Index), new { instanceId = instance.InstanceId });
    }

    [HttpPost]
    [Admin("ai/custom-chat/delete/{instanceId}", "CustomChatDelete")]
    public async Task<IActionResult> Delete(string instanceId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        if (await _instanceManager.DeleteAsync(instanceId))
        {
            await _notifier.SuccessAsync(H["Custom chat instance deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to delete the custom chat instance."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<CustomChatInstanceViewModel> PopulateViewModelAsync(AICustomChatInstance instance, bool isNew)
    {
        var model = new CustomChatInstanceViewModel
        {
            InstanceId = instance.InstanceId,
            Title = instance.Title,
            ConnectionName = instance.ConnectionName,
            DeploymentId = instance.DeploymentId,
            SystemMessage = instance.SystemMessage,
            MaxTokens = instance.MaxTokens,
            Temperature = instance.Temperature,
            TopP = instance.TopP,
            FrequencyPenalty = instance.FrequencyPenalty,
            PresencePenalty = instance.PresencePenalty,
            PastMessagesCount = instance.PastMessagesCount,
            IsNew = isNew,
        };

        await PopulateDropdownsAsync(model, instance);

        return model;
    }

    private async Task PopulateDropdownsAsync(CustomChatInstanceViewModel model, AICustomChatInstance instance = null)
    {
        var connectionNames = new List<SelectListItem>();
        string providerName = null;

        foreach (var provider in _providerOptions.Providers)
        {
            foreach (var connection in provider.Value.Connections)
            {
                var displayName = connection.Value.TryGetValue("ConnectionNameAlias", out var alias)
                    ? alias.ToString()
                    : connection.Key;
                connectionNames.Add(new SelectListItem(displayName, connection.Key));

                if (string.IsNullOrEmpty(providerName))
                {
                    providerName = provider.Key;
                }
            }
        }

        model.ConnectionNames = connectionNames;
        model.ProviderName = providerName;

        var connectionName = model.ConnectionName;
        if (string.IsNullOrEmpty(connectionName) && connectionNames.Count > 0)
        {
            connectionName = connectionNames.First().Value;
        }

        if (!string.IsNullOrEmpty(connectionName) && !string.IsNullOrEmpty(providerName))
        {
            var deployments = await _deploymentManager.GetAllAsync(providerName, connectionName);
            model.Deployments = deployments.Select(d => new SelectListItem(d.Name, d.ItemId));
        }
        else
        {
            model.Deployments = [];
        }

        if (_toolDefinitions.Tools.Count > 0)
        {
            var selectedTools = instance?.ToolNames ?? [];
            model.Tools = _toolDefinitions.Tools
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"].Value)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = selectedTools.Contains(entry.Key),
                }).OrderBy(entry => entry.DisplayText).ToArray());
        }
    }

    private void MapToInstance(CustomChatInstanceViewModel model, AICustomChatInstance instance)
    {
        instance.Title = model.Title?.Trim();
        instance.ConnectionName = model.ConnectionName;
        instance.DeploymentId = model.DeploymentId;
        instance.SystemMessage = model.SystemMessage;
        instance.MaxTokens = model.MaxTokens;
        instance.Temperature = model.Temperature;
        instance.TopP = model.TopP;
        instance.FrequencyPenalty = model.FrequencyPenalty;
        instance.PresencePenalty = model.PresencePenalty;
        instance.PastMessagesCount = model.PastMessagesCount;

        // Set the source based on the first available profile source
        if (string.IsNullOrEmpty(instance.Source) && _aiOptions.ProfileSources.Count > 0)
        {
            instance.Source = _aiOptions.ProfileSources.Keys.First();
        }

        var selectedToolKeys = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId);

        if (selectedToolKeys is null || !selectedToolKeys.Any())
        {
            instance.ToolNames = [];
        }
        else
        {
            instance.ToolNames = _toolDefinitions.Tools.Keys
                .Intersect(selectedToolKeys)
                .ToList();
        }
    }
}
