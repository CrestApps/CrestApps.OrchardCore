using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

internal sealed class AIProfileTemplateAnalyticsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    internal readonly IStringLocalizer S;

    public AIProfileTemplateAnalyticsDisplayDriver(
        IStringLocalizer<AIProfileTemplateAnalyticsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<EditAIProfileAnalyticsViewModel>("AIProfileAnalytics_Edit", model =>
        {
            var metadata = template.As<AnalyticsMetadata>();
            model.EnableSessionMetrics = metadata.EnableSessionMetrics;
            model.EnableConversionMetrics = metadata.EnableConversionMetrics;
            model.ConversionGoals = metadata.ConversionGoals
                .Select(g => new ConversionGoalViewModel
                {
                    Name = g.Name,
                    Description = g.Description,
                    MinScore = g.MinScore,
                    MaxScore = g.MaxScore,
                })
                .ToList();
        }).Location("Content:10#Data Processing & Metrics:15")
        .RenderWhen(() => Task.FromResult(template.ProfileType == AIProfileType.Chat));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.ProfileType != AIProfileType.Chat)
        {
            return Edit(template, context);
        }

        var model = new EditAIProfileAnalyticsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var goals = model.ConversionGoals?.Where(g => !string.IsNullOrWhiteSpace(g.Name)).ToList() ?? [];

        if (model.EnableConversionMetrics)
        {
            if (goals.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConversionGoals), S["At least one conversion goal is required when conversion metrics are enabled."]);
            }

            var duplicateNames = goals
                .GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            foreach (var duplicate in duplicateNames)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConversionGoals), S["Duplicate goal name: '{0}'. Names must be unique.", duplicate]);
            }

            foreach (var goal in goals)
            {
                if (!IsValidKey(goal.Name))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConversionGoals), S["Goal name '{0}' is invalid. Only alphanumeric characters and underscores are allowed.", goal.Name]);
                }

                if (string.IsNullOrWhiteSpace(goal.Description))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConversionGoals), S["Goal '{0}' requires a description.", goal.Name]);
                }

                if (goal.MinScore < 0)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConversionGoals), S["Minimum score for goal '{0}' cannot be negative.", goal.Name]);
                }

                if (goal.MaxScore <= goal.MinScore)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConversionGoals), S["Maximum score for goal '{0}' must be greater than minimum score.", goal.Name]);
                }
            }
        }

        var metadata = template.As<AnalyticsMetadata>();
        metadata.EnableSessionMetrics = model.EnableSessionMetrics;
        metadata.EnableConversionMetrics = model.EnableConversionMetrics;
        metadata.ConversionGoals = goals.Select(g => new ConversionGoal
        {
            Name = g.Name,
            Description = g.Description,
            MinScore = g.MinScore,
            MaxScore = g.MaxScore,
        }).ToList();

        template.Put(metadata);

        return Edit(template, context);
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
