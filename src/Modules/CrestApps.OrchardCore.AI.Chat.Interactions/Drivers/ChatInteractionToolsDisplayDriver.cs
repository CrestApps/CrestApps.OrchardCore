using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

internal sealed class ChatInteractionToolsDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public ChatInteractionToolsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ChatInteractionToolsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ChatInteraction interaction, BuildEditorContext context)
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
            if (await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, tool.Key as object))
            {
                accessibleTools[tool.Key] = tool.Value;
            }
        }

        if (accessibleTools.Count == 0)
        {
            return null;
        }

        return Initialize<EditChatInteractionToolsViewModel>("ChatInteractionTools_Edit", model =>
        {
            model.Tools = accessibleTools
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"].Value)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = interaction.ToolNames?.Contains(entry.Key) ?? false,
                }).OrderBy(entry => entry.DisplayText).ToArray());
        }).Location("Parameters:5#Capabilities:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        var model = new EditChatInteractionToolsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedToolKeys = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId);

        interaction.ToolNames = selectedToolKeys is null || !selectedToolKeys.Any()
            ? []
            : _toolDefinitions.Tools.Keys
                .Intersect(selectedToolKeys)
                .ToList();

        return Edit(interaction, context);
    }
}
