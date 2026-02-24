using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileDataExtractionDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    public AIProfileDataExtractionDisplayDriver(
        IStringLocalizer<AIProfileDataExtractionDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfileDataExtractionViewModel>("AIProfileDataExtraction_Edit", model =>
        {
            var settings = profile.GetSettings<AIProfileDataExtractionSettings>();

            model.EnableDataExtraction = settings.EnableDataExtraction;
            model.ExtractionCheckInterval = settings.ExtractionCheckInterval;
            model.SessionInactivityTimeoutInMinutes = settings.SessionInactivityTimeoutInMinutes;
            model.Entries = settings.DataExtractionEntries
                .Select(e => new DataExtractionEntryViewModel
                {
                    Name = e.Name,
                    Description = e.Description,
                    AllowMultipleValues = e.AllowMultipleValues,
                    IsUpdatable = e.IsUpdatable,
                })
                .ToList();

        }).Location("Content:10#Data Extractions:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AIProfileDataExtractionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Remove entries with empty names (deleted rows).
        var entries = model.Entries?.Where(e => !string.IsNullOrWhiteSpace(e.Name)).ToList() ?? [];

        if (model.EnableDataExtraction)
        {
            if (entries.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Entries), S["At least one extraction entry is required when data extraction is enabled."]);
            }

            var duplicateNames = entries
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            foreach (var duplicate in duplicateNames)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Entries), S["Duplicate entry name: '{0}'. Names must be unique.", duplicate]);
            }

            foreach (var entry in entries)
            {
                if (!IsValidKey(entry.Name))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Entries), S["Entry name '{0}' is invalid. Only alphanumeric characters and underscores are allowed.", entry.Name]);
                }
            }
        }

        if (model.ExtractionCheckInterval < 1)
        {
            model.ExtractionCheckInterval = 1;
        }

        if (model.SessionInactivityTimeoutInMinutes < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SessionInactivityTimeoutInMinutes), S["Session Inactivity Timeout must be at least 1 minute."]);
        }

        profile.AlterSettings<AIProfileDataExtractionSettings>(settings =>
        {
            settings.EnableDataExtraction = model.EnableDataExtraction;
            settings.ExtractionCheckInterval = model.ExtractionCheckInterval;
            settings.SessionInactivityTimeoutInMinutes = model.SessionInactivityTimeoutInMinutes;
            settings.DataExtractionEntries = entries.Select(e => new DataExtractionEntry
            {
                Name = e.Name,
                Description = e.Description,
                AllowMultipleValues = e.AllowMultipleValues,
                IsUpdatable = e.IsUpdatable,
            }).ToList();
        });

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
