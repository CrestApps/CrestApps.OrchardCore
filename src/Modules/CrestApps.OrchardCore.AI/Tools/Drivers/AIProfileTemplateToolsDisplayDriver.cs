using CrestApps.Core;
using CrestApps.Core.AI;
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

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

internal sealed class AIProfileTemplateToolsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateToolsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AIProfileTemplateToolsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfileTemplate template, BuildEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        var user = _httpContextAccessor.HttpContext.User;
        var accessibleTools = new Dictionary<string, AIToolDefinitionEntry>();

        foreach (var tool in _toolDefinitions.Tools)
        {
            if (tool.Value.IsSystemTool)
            {
                continue;
            }

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
            var metadata = template.As<ProfileTemplateMetadata>();
            var selectedNames = metadata.ToolNames ?? [];

            model.Tools = accessibleTools
            .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
            {
                ItemId = entry.Key,
                DisplayText = entry.Value.Title,
                Description = entry.Value.Description,
                IsSelected = selectedNames.Contains(entry.Key),
            }).OrderBy(entry => entry.DisplayText).ToArray());

        }).Location("Content:7#Capabilities;8")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile || _toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        var model = new EditProfileToolsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedToolKeys = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId);

        var metadata = template.As<ProfileTemplateMetadata>();

        if (selectedToolKeys is null || !selectedToolKeys.Any())
        {
            metadata.ToolNames = [];
        }
        else
        {
            metadata.ToolNames = _toolDefinitions.Tools.Keys
                .Intersect(selectedToolKeys)
                .ToArray();
        }

        template.Put(metadata);

        return Edit(template, context);
    }
}
