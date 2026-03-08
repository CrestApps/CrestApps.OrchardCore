using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

internal sealed class AIProfileAnalyticsDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    public AIProfileAnalyticsDisplayDriver(
        IStringLocalizer<AIProfileAnalyticsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileAnalyticsViewModel>("AIProfileAnalytics_Edit", model =>
        {
            var metadata = profile.As<AnalyticsMetadata>();
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
        .RenderWhen(() => Task.FromResult(profile.Type == AIProfileType.Chat));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (profile.Type != AIProfileType.Chat)
        {
            return Edit(profile, context);
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

        var metadata = profile.As<AnalyticsMetadata>();
        metadata.EnableSessionMetrics = model.EnableSessionMetrics;
        metadata.EnableConversionMetrics = model.EnableConversionMetrics;
        metadata.ConversionGoals = goals.Select(g => new ConversionGoal
        {
            Name = g.Name,
            Description = g.Description,
            MinScore = g.MinScore,
            MaxScore = g.MaxScore,
        }).ToList();

        profile.Put(metadata);

        return Edit(profile, context);
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
