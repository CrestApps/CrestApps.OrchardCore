using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.Conditions;
using CrestApps.OrchardCore.ContentFields.Settings;
using CrestApps.OrchardCore.ContentFields.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// Adds <b>Show this field when</b> to the settings of every content field: conditions on the item's other fields and
/// on values modules provide (<see cref="IContentConditionSource"/>).
/// </summary>
internal sealed class ConditionalFieldSettingsDisplayDriver : ContentPartFieldDefinitionDisplayDriver
{
    private const int BlankRows = 2;

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ConditionalFieldsService _service;

    internal readonly IStringLocalizer S;

    public ConditionalFieldSettingsDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        ConditionalFieldsService service,
        IStringLocalizer<ConditionalFieldSettingsDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _service = service;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ContentPartFieldDefinition model, BuildEditorContext context)
    {
        var sources = await GetSourcesAsync(model);
        var rule = model.GetSettings<ConditionalFieldSettings>().ShowWhen;

        return Initialize<ConditionalFieldSettingsViewModel>("ConditionalFieldSettings_Edit", viewModel =>
        {
            viewModel.Match = rule?.Match ?? ConditionMatch.All;
            viewModel.Sources = sources;

            foreach (var condition in rule?.Conditions ?? [])
            {
                if (string.IsNullOrWhiteSpace(condition?.Source))
                {
                    continue;
                }

                var source = sources.FirstOrDefault(item => item.Name == condition.Source);

                if (source is null)
                {
                    viewModel.MissingSources.Add(condition.Source);
                }

                viewModel.Conditions.Add(new ConditionalFieldConditionRow
                {
                    Source = condition.Source,
                    Operator = condition.Operator,
                    Values = source?.Options.Count > 0 ? [.. condition.Values] : [],
                    Text = source?.Options.Count > 0 ? null : string.Join('\n', condition.Values),
                });
            }

            for (var index = 0; index < BlankRows; index++)
            {
                viewModel.Conditions.Add(new ConditionalFieldConditionRow());
            }
        }).Location("Content:30");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition model, UpdatePartFieldEditorContext context)
    {
        var viewModel = new ConditionalFieldSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var sources = (await GetSourcesAsync(model)).ToDictionary(source => source.Name, StringComparer.Ordinal);
        var conditions = new List<Condition>();

        foreach (var row in viewModel.Conditions ?? [])
        {
            if (string.IsNullOrWhiteSpace(row?.Source))
            {
                continue;
            }

            var source = row.Source.Trim();
            var values = sources.TryGetValue(source, out var descriptor) && descriptor.Options.Count > 0
                ? (row.Values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
                : (row.Text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            if (row.Operator is not (ConditionOperator.IsEmpty or ConditionOperator.IsNotEmpty) && values.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, S["Give a value to compare \"{0}\" with.", descriptor?.DisplayName ?? source]);

                continue;
            }

            conditions.Add(new Condition { Source = source, Operator = row.Operator, Values = values });
        }

        var rule = conditions.Count == 0 ? null : new ConditionRule { Match = viewModel.Match, Conditions = conditions };

        // A field that depends on itself, or fields that depend on each other, could never be decided.
        if (rule is not null && await FindCycleAsync(model, rule) is { Count: > 0 } cycle)
        {
            var names = cycle.Select(key => sources.TryGetValue(key, out var source) ? source.DisplayName : model.DisplayName());
            context.Updater.ModelState.AddModelError(Prefix, S["These fields show depending on each other, so none could ever be decided: {0}.", string.Join(", ", names)]);
        }

        context.Builder.WithSettings(new ConditionalFieldSettings { ShowWhen = rule });

        return await EditAsync(model, context);
    }

    // The sources a rule of this field can read: the other fields and module values of every type that uses its part.
    private async Task<List<ContentConditionSourceDescriptor>> GetSourcesAsync(ContentPartFieldDefinition model)
    {
        var sources = new List<ContentConditionSourceDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in await TypesUsingAsync(model))
        {
            var ownKeys = type.Parts.Where(part => part.PartDefinition.Name == model.PartDefinition.Name).Select(part => $"{part.Name}.{model.Name}").ToHashSet(StringComparer.Ordinal);

            foreach (var source in await _service.DescribeSourcesAsync(type))
            {
                if (!ownKeys.Contains(source.Name) && seen.Add(source.Name))
                {
                    sources.Add(source);
                }
            }
        }

        return sources;
    }

    private async Task<List<ContentTypeDefinition>> TypesUsingAsync(ContentPartFieldDefinition model)
    {
        var partName = model.PartDefinition?.Name;

        if (string.IsNullOrEmpty(partName))
        {
            return [];
        }

        return [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync()).Where(type => type.Parts.Any(part => part.PartDefinition.Name == partName))];
    }

    // A cycle on any type that uses the part, with this field's new rule in place of its stored one.
    private async Task<IReadOnlyList<string>> FindCycleAsync(ContentPartFieldDefinition model, ConditionRule rule)
    {
        foreach (var type in await TypesUsingAsync(model))
        {
            var rules = ConditionalFieldsService.GetRules(type);

            foreach (var part in type.Parts.Where(part => part.PartDefinition.Name == model.PartDefinition.Name))
            {
                rules[$"{part.Name}.{model.Name}"] = rule;
            }

            if (ConditionalVisibility.FindCycle(rules) is { Count: > 0 } cycle)
            {
                return cycle;
            }
        }

        return [];
    }
}
