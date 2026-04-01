using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
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
            var analyticsMetadata = template.As<AnalyticsMetadata>();
            model.EnableSessionMetrics = analyticsMetadata.EnableSessionMetrics;
            model.EnableConversionMetrics = analyticsMetadata.EnableConversionMetrics;
            model.ConversionGoals = analyticsMetadata.ConversionGoals
            .Select(g => new ConversionGoalViewModel
            {
                Name = g.Name,
                Description = g.Description,
                MinScore = g.MinScore,
                MaxScore = g.MaxScore,
            })
        .ToList();
        }).Location("Content:15#Data Processing & Metrics;10")
        .RenderWhen(() =>
        {
            if (template.Source != AITemplateSources.Profile)
            {
                return Task.FromResult(false);
            }

            var profileMetadata = template.As<ProfileTemplateMetadata>();
            return Task.FromResult(profileMetadata.ProfileType == AIProfileType.Chat);
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var profileMetadata = template.As<ProfileTemplateMetadata>();

        if (profileMetadata.ProfileType != AIProfileType.Chat)
        {
            return null;
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

        var analyticsMetadata = template.As<AnalyticsMetadata>();
        analyticsMetadata.EnableSessionMetrics = model.EnableSessionMetrics;
        analyticsMetadata.EnableConversionMetrics = model.EnableConversionMetrics;
        analyticsMetadata.ConversionGoals = goals.Select(g => new ConversionGoal
        {
            Name = g.Name,
            Description = g.Description,
            MinScore = g.MinScore,
            MaxScore = g.MaxScore,
        }).ToList();

        template.Put(analyticsMetadata);

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
