using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the content definition recipe step base.
/// </summary>
public abstract class ContentDefinitionRecipeStepBase(
    IEnumerable<IContentSchemaDefinition> schemaDefinitions,
    IContentSchemaProvider contentSchemaProvider) : IRecipeStep
{
    private readonly IEnumerable<IContentSchemaDefinition> _schemaDefinitions = schemaDefinitions;
    private readonly IContentSchemaProvider _contentSchemaProvider = contentSchemaProvider;

    private JsonSchema _cached;

    /// <summary>
    /// Gets the name.
    /// </summary>
    public abstract string Name { get; }

    protected virtual IReadOnlyList<string> RequiredProperties => ["name"];

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var partNames = await _contentSchemaProvider.GetPartNamesAsync(cancellationToken);
        var fieldTypeNames = await _contentSchemaProvider.GetFieldTypeNamesAsync(cancellationToken);

        var partsItem = await CreatePartsItemAsync(partNames, cancellationToken);
        var fieldsItem = await CreateFieldsItemAsync(fieldTypeNames, cancellationToken);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(Name).Description($"Recipe step discriminator. Must be '{Name}'.")),
                ("ContentTypes", BuildContentTypesArray(partsItem)),
                ("ContentParts", BuildContentPartsArray(fieldsItem)))
            .Required(RequiredProperties.ToArray())
            .AnyOf(
                new JsonSchemaBuilder().Required("ContentTypes"),
                new JsonSchemaBuilder().Required("ContentParts"))
            .AdditionalProperties(false)
            .Build();

        return _cached;
    }

    private static JsonSchemaBuilder BuildContentTypesArray(JsonSchemaBuilder partsItem)
    {
        return new JsonSchemaBuilder()
            .Description(
                "List of content types. "
                + "Fields must be attached through a content part whose PartName matches the content type's Name. "
                + "Those parts must also appear in the ContentPartFieldDefinitionRecords inside ContentParts. "
                + "Fields cannot be used directly in PartName.")
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Technical content type name.")),
                    ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Display name shown in the Orchard admin UI.")),
                    ("Settings", TypeSettingsSchema().Description("Content type settings object, including standard Orchard ContentTypeSettings.")),
                    ("ContentTypePartDefinitionRecords", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Array)
                        .Items(partsItem)
                        .Description("Parts attached to this content type.")))
                .Required("Name", "DisplayName", "Settings", "ContentTypePartDefinitionRecords")
                .AdditionalProperties(true));
    }

    private static JsonSchemaBuilder TypeSettingsSchema()
    {
        return new JsonSchemaBuilder().AnyOf(
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("ContentTypeSettings", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Creatable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether editors can create new items of this type.")),
                            ("Listable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether items of this type appear in content lists.")),
                            ("Draftable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether items of this type support drafts.")),
                            ("Versionable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether multiple versions are retained.")),
                            ("Securable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether item-level permissions are enabled.")))
                        .Required("Creatable", "Listable", "Draftable", "Versionable", "Securable")
                        .AdditionalProperties(false)
                        .Description("Standard Orchard Core content type settings.")))
                .AdditionalProperties(true),
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AdditionalProperties(true));
    }

    private static JsonSchemaBuilder BuildContentPartsArray(JsonSchemaBuilder fieldsItem)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Reusable content part name.")),
                    ("Settings", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Part definition settings, including part-specific settings envelopes.")),
                    ("ContentPartFieldDefinitionRecords", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Array)
                        .Items(fieldsItem)
                        .Description("Field definitions that belong to this reusable part.")))
                .Required("Name", "Settings", "ContentPartFieldDefinitionRecords")
                .AdditionalProperties(true));
    }

    private async ValueTask<JsonSchemaBuilder> CreatePartsItemAsync(IEnumerable<string> knownPartNames, CancellationToken cancellationToken)
    {
        var result = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("PartName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .AnyOf(
                        new JsonSchemaBuilder().Enum(knownPartNames),
                        new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(@"^(?!.*Field$).+"))
                    .Description("Attached part type name. Known part names are enumerated when available.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Attachment name used on the content type. Usually matches PartName.")),
                ("Settings", GenericSubSettings("ContentTypePartSettings").Description("Attachment settings for this part on the content type.")))
            .Required("PartName", "Name", "Settings")
            .AdditionalProperties(true);

        var fragments = await GatherFragmentsByNameAsync(ContentDefinitionSchemaType.Part, cancellationToken);

        if (fragments.Count > 0)
        {
            result = result.AllOf(CreateConditionalSettingsSchemas("PartName", fragments).ToArray());
        }

        return result;
    }

    private async ValueTask<JsonSchemaBuilder> CreateFieldsItemAsync(IEnumerable<string> fieldTypeNames, CancellationToken cancellationToken)
    {
        var result = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("FieldName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(fieldTypeNames)
                    .Description("Field type name. Known Orchard field types are enumerated from the current tenant.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Field name used inside the content part.")),
                ("Settings", GenericSubSettings("ContentPartFieldSettings").Description("Field definition settings, including Orchard placement/editor settings and field-specific settings envelopes.")))
            .Required("FieldName", "Name", "Settings")
            .AdditionalProperties(true);

        var fragments = await GatherFragmentsByNameAsync(ContentDefinitionSchemaType.Field, cancellationToken);

        if (fragments.Count > 0)
        {
            result = result.AllOf(CreateConditionalSettingsSchemas("FieldName", fragments).ToArray());
        }

        return result;
    }

    private static JsonSchemaBuilder GenericSubSettings(string key)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (key, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Display label shown in the admin UI.")),
                        ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Administrative description shown to editors.")),
                        ("Position", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Placement position used by Orchard when rendering the part or field.")),
                        ("DisplayMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Display mode selected for the part or field.")),
                        ("Editor", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Editor name used to edit the part or field.")))
                    .AdditionalProperties(false)
                    .Description($"Standard Orchard settings stored under '{key}'.")))
            .AdditionalProperties(true);
    }

    private static IEnumerable<JsonSchemaBuilder> CreateConditionalSettingsSchemas(
        string propertyName,
        IReadOnlyDictionary<string, IReadOnlyList<JsonSchemaBuilder>> fragmentsByName)
    {
        return fragmentsByName
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new JsonSchemaBuilder()
                .If(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties((propertyName, new JsonSchemaBuilder().Const(pair.Key)))
                    .Required(propertyName))
                .Then(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("Settings", MergeFragments(pair.Value)))
                    .AdditionalProperties(true)));
    }

    private async ValueTask<IReadOnlyDictionary<string, IReadOnlyList<JsonSchemaBuilder>>> GatherFragmentsByNameAsync(
        ContentDefinitionSchemaType target,
        CancellationToken cancellationToken)
    {
        var collected = new Dictionary<string, List<JsonSchemaBuilder>>(StringComparer.OrdinalIgnoreCase);

        foreach (var schemaDefinition in _schemaDefinitions)
        {
            if (schemaDefinition.Type == target)
            {
                if (!collected.TryGetValue(schemaDefinition.Name, out var fragments))
                {
                    fragments = [];
                    collected[schemaDefinition.Name] = fragments;
                }

                fragments.Add(await schemaDefinition.GetSettingsSchemaAsync(cancellationToken));
            }
        }

        return collected.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<JsonSchemaBuilder>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static JsonSchemaBuilder MergeFragments(IReadOnlyList<JsonSchemaBuilder> fragments)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AllOf(fragments.ToArray())
            .AdditionalProperties(true);
    }
}
