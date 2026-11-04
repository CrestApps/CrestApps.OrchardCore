using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.SchemaOrg;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Seo.Services;

/// <summary>
/// The result of configuring the schema.org data of a content type.
/// </summary>
internal sealed class SchemaOrgConfigurationResult
{
    /// <summary>
    /// Gets the errors that stopped the change; empty when it was made.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the settings saved.
    /// </summary>
    public SchemaOrgPartSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Schema.org part was added to the content type.
    /// </summary>
    public bool PartAdded { get; set; }

    /// <summary>
    /// Gets or sets the required properties nothing gives yet.
    /// </summary>
    public List<string> MissingRequired { get; set; } = [];
}

/// <summary>
/// Configures the schema.org data of content types in code: adds the Schema.org part, chooses its type and maps its
/// properties, suggesting the mappings that are not given. The AI tools use it.
/// </summary>
internal sealed class SchemaOrgConfigurator
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly SchemaOrgContentSources _contentSources;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaOrgConfigurator"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contentSources">The values of content items that properties can take.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager, which checks Liquid templates.</param>
    public SchemaOrgConfigurator(
        IContentDefinitionManager contentDefinitionManager,
        SchemaOrgContentSources contentSources,
        ILiquidTemplateManager liquidTemplateManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentSources = contentSources;
        _liquidTemplateManager = liquidTemplateManager;
    }

    /// <summary>
    /// Describes a content type for whoever configures it: its current schema.org settings, the values its items can
    /// give, and, for a type, that type's properties and the suggested mappings.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <param name="schemaType">The schema.org type to describe, or <see langword="null"/> for the configured one.</param>
    /// <returns>The description, or <see langword="null"/> when there is no such content type.</returns>
    public async Task<object> DescribeAsync(string contentType, string schemaType)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        if (definition is null)
        {
            return null;
        }

        var part = SchemaOrgPartDefinitions.Find(definition);
        var settings = part?.GetSettings<SchemaOrgPartSettings>();
        var sources = _contentSources.List(definition);
        var type = string.IsNullOrWhiteSpace(schemaType) ? settings?.SchemaType : schemaType.Trim();

        return new
        {
            contentType = definition.Name,
            displayName = definition.DisplayName,
            hasSchemaOrgPart = part is not null,
            schemaType = settings?.SchemaType,
            properties = settings?.Properties ?? [],
            sources = sources.Select(source => new { source.Key, source.Text, kind = source.Kind.ToString() }),
            typeProperties = string.IsNullOrEmpty(type)
                ? null
                : SchemaOrgVocabulary.GetProperties(type).Select(property => new
                {
                    property.Name,
                    kind = property.Kind.ToString(),
                    property.ObjectType,
                    level = property.Level.ToString(),
                    property.Description,
                    options = property.Options.Length == 0 ? null : property.Options.Select(option => option.Value),
                }),
            suggestedProperties = string.IsNullOrEmpty(type) ? null : SchemaOrgSuggestions.SuggestMappings(type, sources),
            automaticProperties = SchemaOrgSuggestions.AutomaticProperties.Keys,
        };
    }

    /// <summary>
    /// Sets the schema.org type and properties of a content type, adding the Schema.org part when it is missing.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <param name="schemaType">The schema.org type, such as <c>BlogPosting</c>.</param>
    /// <param name="properties">The mappings to set, or <see langword="null"/> to keep the existing ones.</param>
    /// <param name="replace">Whether <paramref name="properties"/> replace the existing mappings instead of being merged into them.</param>
    /// <param name="addSuggested">Whether to add the suggested mappings of the properties still unmapped.</param>
    /// <returns>The result.</returns>
    public async Task<SchemaOrgConfigurationResult> ConfigureAsync(
        string contentType,
        string schemaType,
        IEnumerable<SchemaOrgPropertyMapping> properties,
        bool replace,
        bool addSuggested)
    {
        var result = new SchemaOrgConfigurationResult();
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        if (definition is null)
        {
            result.Errors.Add($"There is no content type named '{contentType}'.");

            return result;
        }

        var part = SchemaOrgPartDefinitions.Find(definition);
        var current = part?.GetSettings<SchemaOrgPartSettings>() ?? new SchemaOrgPartSettings();
        var type = string.IsNullOrWhiteSpace(schemaType) ? current.SchemaType : schemaType.Trim();

        if (string.IsNullOrEmpty(type) || !SchemaOrgValueWriter.IsValidTypeName(type))
        {
            result.Errors.Add("The schema.org type must be a single type name, such as Article or Event.");

            return result;
        }

        var sources = _contentSources.List(definition);
        var sourceKeys = new HashSet<string>(sources.Select(source => source.Key), StringComparer.Ordinal);
        var mappings = replace ? [] : (current.Properties ?? []).ToList();

        foreach (var mapping in properties ?? [])
        {
            var name = mapping?.Name?.Trim();

            if (!SchemaOrgValueWriter.IsValidPropertyName(name))
            {
                result.Errors.Add($"'{mapping?.Name}' is not a valid property name. Use schema.org names, such as headline, or dotted names, such as offers.price.");

                continue;
            }

            if (mapping.Source == SchemaOrgValueSource.Content && !sourceKeys.Contains(mapping.Value ?? string.Empty))
            {
                result.Errors.Add($"The property '{name}' takes its value from '{mapping.Value}', which this content type does not have. Use one of the listed sources.");

                continue;
            }

            if (mapping.Source == SchemaOrgValueSource.Liquid && !_liquidTemplateManager.Validate(mapping.Value ?? string.Empty, out var errors))
            {
                result.Errors.Add($"The Liquid of the property '{name}' is not valid: {string.Join(' ', errors)}");

                continue;
            }

            mappings.RemoveAll(existing => existing.Name == name);
            mappings.Add(new SchemaOrgPropertyMapping
            {
                Name = name,
                Source = mapping.Source,
                Value = mapping.Source == SchemaOrgValueSource.Editor ? null : mapping.Value?.Trim(),
            });
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        if (addSuggested)
        {
            mappings.AddRange(SchemaOrgSuggestions.SuggestMappings(type, sources, mappings));
        }

        var settings = new SchemaOrgPartSettings
        {
            SchemaType = type,
            Properties = mappings,
        };

        await _contentDefinitionManager.AlterTypeDefinitionAsync(definition.Name, builder => builder
            .WithPart(nameof(SchemaOrgPart), partBuilder => partBuilder.WithSettings(settings)));

        result.PartAdded = part is null;
        result.Settings = settings;
        result.MissingRequired = SchemaOrgSuggestions.MissingRequired(type, mappings);

        return result;
    }
}
