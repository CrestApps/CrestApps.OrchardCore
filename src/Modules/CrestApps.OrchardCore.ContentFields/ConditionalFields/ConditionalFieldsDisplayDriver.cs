using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.ViewModels;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// Two jobs on every content editor. Before the field editors read the post (item-level drivers run first), it keeps a
/// copy of what the item holds, which <see cref="ConditionalFieldsDisplayHandler"/> gives back to hidden fields. And on
/// an editor whose type has field rules, it adds the rules for the browser, which shows and hides fields as people
/// answer.
/// </summary>
internal sealed class ConditionalFieldsDisplayDriver : ContentDisplayDriver
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ConditionalFieldsService _service;
    private readonly ConditionalFieldsSnapshots _snapshots;

    public ConditionalFieldsDisplayDriver(IContentDefinitionManager contentDefinitionManager, ConditionalFieldsService service, ConditionalFieldsSnapshots snapshots)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _service = service;
        _snapshots = snapshots;
    }

    public override async Task<IDisplayResult> EditAsync(ContentItem model, BuildEditorContext context)
    {
        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(model.ContentType);
        var rules = ConditionalFieldsService.GetRules(type);

        if (rules.Count == 0)
        {
            return null;
        }

        var descriptors = (await _service.DescribeSourcesAsync(type)).ToDictionary(descriptor => descriptor.Name, StringComparer.Ordinal);
        var used = rules.Values.SelectMany(ConditionalVisibility.Sources).Where(descriptors.ContainsKey).Distinct(StringComparer.Ordinal).ToList();
        var values = await _service.ReadSourcesAsync(model, type, used);
        var ancestors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var sources = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var name in used)
        {
            var descriptor = descriptors[name];

            foreach (var (value, above) in descriptor.Ancestors)
            {
                ancestors.TryAdd(value, above);
            }

            sources[name] = new
            {
                kind = descriptor.Kind,
                input = string.IsNullOrEmpty(descriptor.InputName) ? null : Prefixed(context.HtmlFieldPrefix, descriptor.InputName),
                values = values.TryGetValue(name, out var current) ? current.Values : [],
            };
        }

        var json = JsonSerializer.Serialize(new
        {
            prefix = context.HtmlFieldPrefix ?? string.Empty,
            fields = rules.Select(rule => new { key = rule.Key, rule = rule.Value }),
            sources,
            ancestors,
        }, _jsonOptions);

        return Initialize<ConditionalFieldsEditorViewModel>("ConditionalFields_Edit", viewModel => viewModel.Json = json)
            .Location("Content:after");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentItem model, UpdateEditorContext context)
    {
        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(model.ContentType);

        if (ConditionalFieldsService.GetRules(type).Count > 0 || _service.HasVisibilityProviders)
        {
            _snapshots.Take(model);
        }

        return await EditAsync(model, context);
    }

    private static string Prefixed(string prefix, string name)
        => string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";
}
