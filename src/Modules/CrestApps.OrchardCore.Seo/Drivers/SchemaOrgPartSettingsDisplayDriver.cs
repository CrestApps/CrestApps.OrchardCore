using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.SchemaOrg;
using CrestApps.OrchardCore.Seo.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Seo.Drivers;

/// <summary>
/// The settings of the Schema.org part on a content type: its schema.org type and where each property comes from. The
/// editor lists the types and, for the chosen one, its properties with the value each should most likely take.
/// </summary>
internal sealed class SchemaOrgPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<SchemaOrgPart>
{
    /// <summary>
    /// The serializer options of the JSON the editor's script reads and writes.
    /// </summary>
    internal static readonly JsonSerializerOptions EditorJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SchemaOrgContentSources _contentSources;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaOrgPartSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentSources">The values of content items that properties can take.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager, which checks Liquid templates.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SchemaOrgPartSettingsDisplayDriver(
        SchemaOrgContentSources contentSources,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<SchemaOrgPartSettingsDisplayDriver> stringLocalizer)
    {
        _contentSources = contentSources;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<SchemaOrgPartSettingsViewModel>("SchemaOrgPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<SchemaOrgPartSettings>();

            model.SchemaType = settings.SchemaType;
            model.Properties = JsonSerializer.Serialize(settings.Properties ?? [], EditorJsonOptions);
            model.EditorData = BuildEditorData(contentTypePartDefinition.ContentTypeDefinition);
        }).Location("Content");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new SchemaOrgPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var schemaType = model.SchemaType?.Trim();

        if (!string.IsNullOrEmpty(schemaType) && !SchemaOrgValueWriter.IsValidTypeName(schemaType))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SchemaType), S["The schema.org type must be a single type name, such as Article or Event."]);
        }

        var properties = ReadProperties(model.Properties);

        if (properties is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Properties), S["The properties could not be read. Reload the page and try again."]);
            properties = [];
        }

        foreach (var property in properties)
        {
            if (!SchemaOrgValueWriter.IsValidPropertyName(property.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Properties), S["'{0}' is not a valid property name. Use schema.org names, such as headline, or dotted names, such as offers.price.", property.Name ?? string.Empty]);
            }
            else if (property.Source == SchemaOrgValueSource.Liquid &&
                !string.IsNullOrWhiteSpace(property.Value) &&
                !_liquidTemplateManager.Validate(property.Value, out var errors))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Properties), S["The Liquid of the property '{0}' is not valid: {1}", property.Name, string.Join(' ', errors)]);
            }
        }

        context.Builder.WithSettings(new SchemaOrgPartSettings
        {
            SchemaType = string.IsNullOrEmpty(schemaType) ? null : schemaType,
            Properties = properties,
        });

        return Edit(contentTypePartDefinition, context);
    }

    /// <summary>
    /// Reads the property mappings the editor's script posts, dropping the empty rows and the values a source does not
    /// use.
    /// </summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The mappings, or <see langword="null"/> when the JSON cannot be read.</returns>
    internal static List<SchemaOrgPropertyMapping> ReadProperties(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        List<SchemaOrgPropertyMapping> mappings;

        try
        {
            mappings = JsonSerializer.Deserialize<List<SchemaOrgPropertyMapping>>(json, EditorJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        var result = new List<SchemaOrgPropertyMapping>();

        foreach (var mapping in mappings ?? [])
        {
            var name = mapping?.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            result.Add(new SchemaOrgPropertyMapping
            {
                Name = name,
                Source = mapping.Source,
                Value = mapping.Source == SchemaOrgValueSource.Editor || string.IsNullOrWhiteSpace(mapping.Value)
                    ? null
                    : mapping.Value.Trim(),
            });
        }

        return result;
    }

    private string BuildEditorData(ContentTypeDefinition contentTypeDefinition)
    {
        var types = SchemaOrgVocabulary.Types
            .Select(type => new
            {
                type.Name,
                type.Group,
                type.Description,
                type.GuideUrl,
                Properties = SchemaOrgVocabulary.GetProperties(type.Name),
            });

        var data = new
        {
            Types = types,
            FallbackProperties = SchemaOrgVocabulary.GetProperties("Thing"),
            Sources = _contentSources.List(contentTypeDefinition),
        };

        return JsonSerializer.Serialize(data, EditorJsonOptions);
    }
}
