using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileTemplateDataExtractionDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    internal readonly IStringLocalizer S;

    public AIProfileTemplateDataExtractionDisplayDriver(
        IStringLocalizer<AIProfileTemplateDataExtractionDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIProfileDataExtractionViewModel>("AIProfileDataExtraction_Edit", model =>
        {
            var settings = template.As<AIProfileDataExtractionSettings>();

            model.EnableDataExtraction = settings.EnableDataExtraction;
            model.ExtractionCheckInterval = settings.ExtractionCheckInterval;
            model.Entries = settings.DataExtractionEntries
                .Select(e => new DataExtractionEntryViewModel
                {
                    Name = e.Name,
                    Description = e.Description,
                    AllowMultipleValues = e.AllowMultipleValues,
                    IsUpdatable = e.IsUpdatable,
                })
                .ToList();
        }).Location("Content:10#Data Processing & Metrics:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        var model = new AIProfileDataExtractionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

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

        var dataExtractionSettings = template.As<AIProfileDataExtractionSettings>();
        dataExtractionSettings.EnableDataExtraction = model.EnableDataExtraction;
        dataExtractionSettings.ExtractionCheckInterval = model.ExtractionCheckInterval;
        dataExtractionSettings.DataExtractionEntries = entries.Select(e => new DataExtractionEntry
        {
            Name = e.Name,
            Description = e.Description,
            AllowMultipleValues = e.AllowMultipleValues,
            IsUpdatable = e.IsUpdatable,
        }).ToList();
        template.Put(dataExtractionSettings);

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
