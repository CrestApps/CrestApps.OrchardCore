using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using CrestApps.Core;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

internal sealed class AIProfileToolsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public AIProfileToolsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AIProfileToolsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfile profile, BuildEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        // Filter tools based on user permissions
        var user = _httpContextAccessor.HttpContext.User;
        var accessibleTools = new Dictionary<string, AIToolDefinitionEntry>();

        foreach (var tool in _toolDefinitions.Tools)
        {
            // Exclude system tools — they are auto-included by the orchestrator.
            if (tool.Value.IsSystemTool)
            {
                continue;
            }

            // Check if user has access to this tool
            if (await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, tool.Key as object))
            {
                accessibleTools[tool.Key] = tool.Value;
            }
        }

        if (accessibleTools.Count == 0)
        {
            return null;
        }

        return Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", model =>
        {
            var selectedNames = GetSelectedToolNames(profile);

            model.Tools = accessibleTools
            .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
            {
                ItemId = entry.Key,
                DisplayText = entry.Value.Title,
                Description = entry.Value.Description,
                IsSelected = selectedNames?.Contains(entry.Key) ?? false,
            }).OrderBy(entry => entry.DisplayText).ToArray());

        }).Location("Content:7#Capabilities;8");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        var model = new EditProfileToolsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedToolKeys = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId);

        var metadata = new FunctionInvocationMetadata();

        if (selectedToolKeys is null || !selectedToolKeys.Any())
        {
            metadata.Names = [];
        }
        else
        {
            metadata.Names = _toolDefinitions.Tools.Keys
                .Intersect(selectedToolKeys)
                .ToArray();
        }

        profile.Put(metadata);

        // Remove the legacy property key if it exists to complete migration.
        profile.Properties.Remove("AIProfileFunctionInvocationMetadata");

        return Edit(profile, context);
    }
    /// <summary>
    /// Reads tool names from the current key, falling back to the legacy key for backward compatibility.
    /// </summary>
    private static string[] GetSelectedToolNames(AIProfile profile)
    {
        var metadata = profile.As<FunctionInvocationMetadata>();

        if (metadata.Names is { Length: > 0 })
        {
            return metadata.Names;
        }

        // Fall back to the legacy property key used in earlier versions.
        var legacyMetadata = profile.Get<FunctionInvocationMetadata>("AIProfileFunctionInvocationMetadata");

        if (legacyMetadata?.Names is { Length: > 0 })
        {
            return legacyMetadata.Names;
        }

        return null;
    }
}
