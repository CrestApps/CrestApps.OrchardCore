using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for the AI profile data extraction shape.
/// </summary>
public sealed class AIProfileDataExtractionDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileDataExtractionDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
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
            var mappings = profile.GetSettings<AIProfileContactFieldMappingSettings>().Mappings
                .Where(mapping => !string.IsNullOrEmpty(mapping.FieldName))
                .GroupBy(mapping => mapping.FieldName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            model.EnableDataExtraction = settings.EnableDataExtraction;
            model.ExtractionCheckInterval = settings.ExtractionCheckInterval;
            model.Entries = settings.DataExtractionEntries
            .Select(e => new DataExtractionEntryViewModel
            {
                Name = e.Name,
                Description = e.Description,
                AllowMultipleValues = e.AllowMultipleValues,
                IsUpdatable = e.IsUpdatable,
                FieldType = e.FieldType,
                AllowedValues = string.Join(Environment.NewLine, e.AllowedValues ?? []),
                ContactField = mappings.TryGetValue(e.Name ?? string.Empty, out var mapping) ? mapping.ContactField : null,
                ContactFieldMode = mapping?.Mode ?? AIContactFieldMappingMode.Overwrite,
            })
        .ToList();
        }).Location("Content:5#Data Processing & Metrics;10");
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

                if (entry.FieldType is DataExtractionFieldType.Enum && SplitLines(entry.AllowedValues).Count == 0)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Entries), S["Entry '{0}' is a choice: list the values it allows.", entry.Name]);
                }

                if (!string.IsNullOrWhiteSpace(entry.ContactField) && entry.ContactField.Trim().Split('.').Length != 2)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Entries), S["Entry '{0}': write the contact field as PartName.FieldName, such as Contact.Company.", entry.Name]);
                }
            }
        }

        if (model.ExtractionCheckInterval < 1)
        {
            model.ExtractionCheckInterval = 1;
        }

        profile.AlterSettings<AIProfileDataExtractionSettings>(settings =>
        {
            settings.EnableDataExtraction = model.EnableDataExtraction;
            settings.ExtractionCheckInterval = model.ExtractionCheckInterval;
            settings.DataExtractionEntries = entries.Select(e => new DataExtractionEntry
            {
                Name = e.Name,
                Description = e.Description,
                AllowMultipleValues = e.AllowMultipleValues || e.FieldType == DataExtractionFieldType.List,
                IsUpdatable = e.IsUpdatable,
                FieldType = e.FieldType,
                AllowedValues = e.FieldType is DataExtractionFieldType.Enum or DataExtractionFieldType.List ? SplitLines(e.AllowedValues) : [],
            }).ToList();
        });

        profile.AlterSettings<AIProfileContactFieldMappingSettings>(settings =>
        {
            settings.Mappings = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.ContactField))
                .Select(e => new AIContactFieldMapping { FieldName = e.Name, ContactField = e.ContactField.Trim(), Mode = e.ContactFieldMode })
                .ToList();
        });

        return Edit(profile, context);
    }

    private static List<string> SplitLines(string text)
        => (text ?? string.Empty)
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
