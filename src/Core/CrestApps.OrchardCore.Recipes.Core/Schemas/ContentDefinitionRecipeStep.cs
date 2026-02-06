using CrestApps.OrchardCore.Recipes.Core.Schemas;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Schema for the "ContentDefinition" recipe step.
/// Composes part and field schemas from the registered <see cref="IContentDefinitionSchemaDefinition"/>
/// services and uses <see cref="IContentSchemaProvider"/> for dynamic enum values.
/// </summary>
public sealed class ContentDefinitionRecipeStep : IRecipeStep
{
    private readonly IEnumerable<IContentDefinitionSchemaDefinition> _schemaDefs;
    private readonly IContentSchemaProvider _contentProvider;
    private JsonSchema _cached;

    public string Name => "ContentDefinition";

    public ContentDefinitionRecipeStep(
        IEnumerable<IContentDefinitionSchemaDefinition> schemaDefs,
        IContentSchemaProvider contentProvider)
    {
        _schemaDefs = schemaDefs;
        _contentProvider = contentProvider;
    }

    public async ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var partNames = await _contentProvider.GetPartNamesAsync();
        var fieldTypeNames = await _contentProvider.GetFieldTypeNamesAsync();

        var partsItem = await CreatePartsItemAsync(partNames);
        var fieldsItem = await CreateFieldsItemAsync(fieldTypeNames);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ContentDefinition")),
                ("ContentTypes", BuildContentTypesArray(partsItem)),
                ("ContentParts", BuildContentPartsArray(fieldsItem)))
            .Required("name", "ContentTypes")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }

    // ── ContentTypes array ──────────────────────────────────

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
                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                    ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                    ("Settings", TypeSettingsSchema()),
                    ("ContentTypePartDefinitionRecords", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Array)
                        .Items(partsItem)))
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
                            ("Creatable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                            ("Listable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                            ("Draftable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                            ("Versionable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                            ("Securable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                        .Required("Creatable", "Listable", "Draftable", "Versionable", "Securable")
                        .AdditionalProperties(false)))
                .AdditionalProperties(true),
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AdditionalProperties(true));
    }

    // ── ContentParts array ──────────────────────────────────

    private static JsonSchemaBuilder BuildContentPartsArray(JsonSchemaBuilder fieldsItem)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                    ("Settings", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)),
                    ("ContentPartFieldDefinitionRecords", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Array)
                        .Items(fieldsItem)))
                .Required("Name", "Settings", "ContentPartFieldDefinitionRecords")
                .AdditionalProperties(true));
    }

    // ── Parts-item sub-schema ───────────────────────────────

    private async ValueTask<JsonSchemaBuilder> CreatePartsItemAsync(IEnumerable<string> knownPartNames)
    {
        var result = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("PartName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .AnyOf(
                        new JsonSchemaBuilder().Enum(knownPartNames),
                        new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(@"^(?!.*Field$).+"))),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Settings", GenericSubSettings("ContentTypePartSettings")))
            .Required("PartName", "Name", "Settings")
            .AdditionalProperties(true);

        var fragments = await GatherFragmentsAsync(ContentDefinitionSchemaType.Part);
        if (fragments.Count > 0)
        {
            result = result.Properties(("Settings", new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AllOf(fragments)
                .AdditionalProperties(true)));
        }

        return result;
    }

    // ── Fields-item sub-schema ──────────────────────────────

    private async ValueTask<JsonSchemaBuilder> CreateFieldsItemAsync(IEnumerable<string> fieldTypeNames)
    {
        var result = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("FieldName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(fieldTypeNames)),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Settings", GenericSubSettings("ContentPartFieldSettings")))
            .Required("FieldName", "Name", "Settings")
            .AdditionalProperties(true);

        var fragments = await GatherFragmentsAsync(ContentDefinitionSchemaType.Field);
        if (fragments.Count > 0)
        {
            result = result.Properties(("Settings", new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AllOf(fragments)
                .AdditionalProperties(true)));
        }

        return result;
    }

    // ── Shared helpers ──────────────────────────────────────

    private static JsonSchemaBuilder GenericSubSettings(string key)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (key, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Position", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("DisplayMode", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Editor", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    private async ValueTask<List<JsonSchema>> GatherFragmentsAsync(ContentDefinitionSchemaType target)
    {
        var collected = new List<JsonSchema>();
        foreach (var def in _schemaDefs)
        {
            if (def.Type == target)
            {
                collected.Add(await def.GetSettingsSchemaAsync());
            }
        }

        return collected;
    }
}
