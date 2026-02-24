using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfilePostSessionDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    public AIProfilePostSessionDisplayDriver(
        IStringLocalizer<AIProfilePostSessionDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
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
                    Options = t.Options.Count > 0 ? string.Join("\n", t.Options) : string.Empty,
                    IsRequired = t.IsRequired,
                })
                .ToList();

        }).Location("Content:10#Post-Session Processing:10");
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

                if (task.Type == PostSessionTaskType.Disposition)
                {
                    var options = ParseOptions(task.Options);
                    if (options.Count == 0)
                    {
                        context.Updater.ModelState.AddModelError(Prefix, nameof(model.Tasks), S["Disposition task '{0}' requires at least one option.", task.Name]);
                    }
                }
            }
        }

        profile.AlterSettings<AIProfilePostSessionSettings>(settings =>
        {
            settings.EnablePostSessionProcessing = model.EnablePostSessionProcessing;
            settings.PostSessionTasks = tasks.Select(t => new PostSessionTask
            {
                Name = t.Name,
                Type = t.Type,
                Instructions = t.Instructions,
                Options = ParseOptions(t.Options),
                IsRequired = t.IsRequired,
            }).ToList();
        });

        return Edit(profile, context);
    }

    private static List<string> ParseOptions(string options)
    {
        if (string.IsNullOrWhiteSpace(options))
        {
            return [];
        }

        return options
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToList();
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
