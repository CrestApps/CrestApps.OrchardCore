using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

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
            // Check if user has access to this tool
            if (await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, tool.Key))
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
            var metadata = profile.As<AIProfileFunctionInvocationMetadata>();

            model.Tools = accessibleTools
            .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
            {
                ItemId = entry.Key,
                DisplayText = entry.Value.Title,
                Description = entry.Value.Description,
                IsSelected = metadata.Names?.Contains(entry.Key) ?? false,
            }).OrderBy(entry => entry.DisplayText).ToArray());

        }).Location("Content:8#Capabilities:5");
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

        var metadata = new AIProfileFunctionInvocationMetadata();

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

        return Edit(profile, context);
    }
}
