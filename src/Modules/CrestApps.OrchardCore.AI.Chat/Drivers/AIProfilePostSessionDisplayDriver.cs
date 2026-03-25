using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfilePostSessionDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public AIProfilePostSessionDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AIProfilePostSessionDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfile profile, BuildEditorContext context)
    {
        var accessibleTools = await GetAccessibleToolsAsync();

        return Initialize<AIProfilePostSessionViewModel>("AIProfilePostSession_Edit", model =>
        {
            var settings = profile.GetSettings<AIProfilePostSessionSettings>();

            model.EnablePostSessionProcessing = settings.EnablePostSessionProcessing;
            model.Tasks = settings.PostSessionTasks
                .Select(t => new PostSessionTaskViewModel
                {
                    Name = t.Name,
                    Type = t.Type,
                    Instructions = t.Instructions,
                    AllowMultipleValues = t.AllowMultipleValues,
                    Options = t.Options
                        .Select(o => new PostSessionTaskOptionViewModel
                        {
                            Value = o.Value,
                            Description = o.Description,
                        })
                        .ToList(),
                })
                .ToList();

            if (accessibleTools.Count > 0)
            {
                var selectedToolNames = settings.ToolNames ?? [];

                model.PostSessionTools = accessibleTools
                    .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Select(entry => new PostSessionToolEntry
                    {
                        ItemId = entry.Key,
                        DisplayText = entry.Value.Title,
                        Description = entry.Value.Description,
                        IsSelected = selectedToolNames.Contains(entry.Key),
                    }).OrderBy(entry => entry.DisplayText).ToArray());
            }
        }).Location("Content:10#Data Processing & Metrics;10");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AIProfilePostSessionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Remove entries with empty names (deleted rows).
        var tasks = model.Tasks?.Where(t => !string.IsNullOrWhiteSpace(t.Name)).ToList() ?? [];

        if (model.EnablePostSessionProcessing)
        {
            if (tasks.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Tasks), S["At least one post-session task is required when post-session processing is enabled."]);
            }

            var duplicateNames = tasks
                .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            foreach (var duplicate in duplicateNames)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Tasks), S["Duplicate task name: '{0}'. Names must be unique.", duplicate]);
            }

            foreach (var task in tasks)
            {
                if (!IsValidKey(task.Name))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Tasks), S["Task name '{0}' is invalid. Only alphanumeric characters and underscores are allowed.", task.Name]);
                }

                // Clean up options: remove entries with empty values.
                task.Options = task.Options?.Where(o => !string.IsNullOrWhiteSpace(o.Value)).ToList() ?? [];

                if (task.Type == PostSessionTaskType.PredefinedOptions)
                {
                    if (task.Options.Count == 0)
                    {
                        context.Updater.ModelState.AddModelError(Prefix, nameof(model.Tasks), S["Task '{0}' requires at least one option when using Predefined Options type.", task.Name]);
                    }

                    // Check for duplicate option values within a task.
                    var duplicateOptions = task.Options
                        .GroupBy(o => o.Value, StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                    foreach (var duplicate in duplicateOptions)
                    {
                        context.Updater.ModelState.AddModelError(Prefix, nameof(model.Tasks), S["Duplicate option value '{0}' in task '{1}'. Option values must be unique.", duplicate, task.Name]);
                    }
                }
            }
        }

        // Collect selected tool names from the posted model.
        var selectedToolKeys = model.PostSessionTools?.Values?.SelectMany(x => x)?.Where(x => x.IsSelected).Select(x => x.ItemId);
        var toolNames = Array.Empty<string>();

        if (selectedToolKeys is not null && selectedToolKeys.Any())
        {
            toolNames = _toolDefinitions.Tools.Keys
                .Intersect(selectedToolKeys)
                .ToArray();
        }

        profile.AlterSettings<AIProfilePostSessionSettings>(settings =>
        {
            settings.EnablePostSessionProcessing = model.EnablePostSessionProcessing;
            settings.ToolNames = toolNames;
            settings.PostSessionTasks = tasks.Select(t => new PostSessionTask
            {
                Name = t.Name,
                Type = t.Type,
                Instructions = t.Instructions,
                AllowMultipleValues = t.AllowMultipleValues,
                Options = t.Type == PostSessionTaskType.PredefinedOptions
                    ? t.Options.Select(o => new PostSessionTaskOption
                    {
                        Value = o.Value,
                        Description = o.Description,
                    }).ToList()
                    : [],
            }).ToList();
        });

        return await EditAsync(profile, context);
    }

    private async Task<Dictionary<string, AIToolDefinitionEntry>> GetAccessibleToolsAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var accessibleTools = new Dictionary<string, AIToolDefinitionEntry>();

        foreach (var tool in _toolDefinitions.Tools)
        {
            if (tool.Value.IsSystemTool)
            {
                continue;
            }

            if (user is not null && await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, tool.Key as object))
            {
                accessibleTools[tool.Key] = tool.Value;
            }
        }

        return accessibleTools;
    }

    private static bool IsValidKey(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
