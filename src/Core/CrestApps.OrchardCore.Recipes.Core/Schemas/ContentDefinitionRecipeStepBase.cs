using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public abstract class ContentDefinitionRecipeStepBase(
    IEnumerable<IContentDefinitionSchemaDefinition> schemaDefinitions,
    IContentSchemaProvider contentSchemaProvider) : IRecipeStep
{
    private readonly IEnumerable<IContentDefinitionSchemaDefinition> _schemaDefinitions = schemaDefinitions;
    private readonly IContentSchemaProvider _contentSchemaProvider = contentSchemaProvider;
    private JsonSchema _cached;

    public abstract string Name { get; }

    protected virtual IReadOnlyList<string> RequiredProperties => ["name"];

    public async ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var partNames = await _contentSchemaProvider.GetPartNamesAsync();
        var fieldTypeNames = await _contentSchemaProvider.GetFieldTypeNamesAsync();

        var partsItem = await CreatePartsItemAsync(partNames);
        var fieldsItem = await CreateFieldsItemAsync(fieldTypeNames);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(Name)),
                ("ContentTypes", BuildContentTypesArray(partsItem)),
                ("ContentParts", BuildContentPartsArray(fieldsItem)))
            .Required(RequiredProperties.ToArray())
            .AdditionalProperties(true)
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
                .AllOf(fragments.ToArray())
                .AdditionalProperties(true)));
        }

        return result;
    }

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
                .AllOf(fragments.ToArray())
                .AdditionalProperties(true)));
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
                        ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Position", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("DisplayMode", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Editor", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    private async ValueTask<List<JsonSchemaBuilder>> GatherFragmentsAsync(ContentDefinitionSchemaType target)
    {
        var collected = new List<JsonSchemaBuilder>();
        foreach (var schemaDefinition in _schemaDefinitions)
        {
            if (schemaDefinition.Type == target)
            {
                collected.Add(await schemaDefinition.GetSettingsSchemaAsync());
            }
        }

        return collected;
    }
}
