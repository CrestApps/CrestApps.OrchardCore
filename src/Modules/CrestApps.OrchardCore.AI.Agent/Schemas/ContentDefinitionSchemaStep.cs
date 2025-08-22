using CrestApps.OrchardCore.AI.Agent.Services;
using Json.Schema;
using Parlot.Fluent;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class ContentDefinitionSchemaStep : IRecipeStep
{
    private readonly IEnumerable<IContentDefinitionSchemaDefinition> _contentPartDefinitionSchemaDefinitions;
    private readonly ContentMetadataService _contentMetadataService;

    private JsonSchema _schema;

    public string Name => "ContentDefinition";

    public ContentDefinitionSchemaStep(
        IEnumerable<IContentDefinitionSchemaDefinition> contentPartDefinitionSchemaDefinitions,
        ContentMetadataService contentMetadataService)
    {
        _contentPartDefinitionSchemaDefinitions = contentPartDefinitionSchemaDefinitions;
        _contentMetadataService = contentMetadataService;
    }

    public async ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_schema != null)
        {
            return _schema;
        }

        // Load known parts and field types from the content metadata service
        var parts = await _contentMetadataService.GetPartsAsync();
        var fieldTypes = await _contentMetadataService.GetFieldsAsync();

        var builder = new JsonSchemaBuilder();

        var partsSchemaBuilder = await GetPartsSchemaBuilderAsync(parts);

        var fieldSchemaBuilder = await GetFieldsSchemaBuilderAsync(fieldTypes);

        builder
            .Type(SchemaValueType.Object) // Root object
            .Properties(
                // $.name — must always be "ContentDefinition"
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("ContentDefinition")
                ),

                // $.ContentTypes — array of content type definitions
                ("ContentTypes", new JsonSchemaBuilder()
                    .Description(
                        """
                    List of content types.
                    If fields need to be added to a content type, they must be included as part of a content part
                    whose PartName matches the content type's Name exactly.
                    These parts must also appear in the ContentPartFieldDefinitionRecords collection inside ContentParts.
                    Note that Fields cannot be treated as Parts, so they cannot be used directly in PartName.
                    Fields must only be attached to a Part either by creating a new reusable part or by placing the field on a private part named exactly as the content type.
                    """
                    )
                    .Type(SchemaValueType.Array)
                    .Items(
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .Properties(
                                // $.ContentTypes[].Name
                                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                // $.ContentTypes[].DisplayName
                                ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                // $.ContentTypes[].Settings — known ContentTypeSettings or any object
                                ("Settings", new JsonSchemaBuilder()
                                    .AnyOf(
                                        // Known structure: ContentTypeSettings object
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
                                                        ("Securable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean))
                                                    )
                                                    .Required("Creatable", "Listable", "Draftable", "Versionable", "Securable")
                                                    .AdditionalProperties(false)
                                                )
                                            )
                                            .AdditionalProperties(true),

                                        // Fallback: any object
                                        new JsonSchemaBuilder()
                                            .Type(SchemaValueType.Object)
                                            .AdditionalProperties(true)
                                    )
                                ),

                                // $.ContentTypes[].ContentTypePartDefinitionRecords — parts attached to the type
                                ("ContentTypePartDefinitionRecords", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Array)
                                    .Items(partsSchemaBuilder)
                                )
                            )
                            .Required("Name", "DisplayName", "Settings", "ContentTypePartDefinitionRecords")
                            .AdditionalProperties(true)
                    )
                ),

                // $.ContentParts — optional array of reusable content part definitions
                ("ContentParts", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .Properties(
                                // $.ContentParts[].Name
                                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                // $.ContentParts[].Settings
                                ("Settings", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .AdditionalProperties(true)
                                ),

                                // $.ContentParts[].ContentPartFieldDefinitionRecords — fields attached to the part
                                ("ContentPartFieldDefinitionRecords", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Array)
                                    .Items(fieldSchemaBuilder)
                                )
                            )
                            .Required("Name", "Settings", "ContentPartFieldDefinitionRecords")
                            .AdditionalProperties(true)
                    )
                )
            )
            // $.name and $.ContentTypes are mandatory; $.ContentParts is optional
            .Required("name", "ContentTypes")
            .AdditionalProperties(true);

        _schema = builder.Build();

        return _schema;
    }

    private async ValueTask<JsonSchemaBuilder> GetPartsSchemaBuilderAsync(IEnumerable<ContentPartMetadata> parts)
    {
        // Base schema for a content type part
        var partsSchemaBuilder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                // $.ContentTypes[].ContentTypePartDefinitionRecords[].PartName
                ("PartName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .AnyOf(
                        new JsonSchemaBuilder().Enum(parts.Select(part => part.Name)), // Known parts
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Pattern(@"^(?!.*Field$).+") // Disallow names ending with 'Field'
                    )
                ),

                // $.ContentTypes[].ContentTypePartDefinitionRecords[].Name
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                // $.ContentTypes[].ContentTypePartDefinitionRecords[].Settings
                ("Settings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ContentTypePartSettings", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .Properties(
                                ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("Position", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("DisplayMode", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("Editor", new JsonSchemaBuilder().Type(SchemaValueType.String))
                            )
                            .AdditionalProperties(false) // no extra keys under ContentTypePartSettings
                        )
                    )
                    .AdditionalProperties(true)
                )
            )
            .Required("PartName", "Name", "Settings")
            .AdditionalProperties(true);

        // Merge all dynamic part settings into Settings
        var allSettingsSchemas = new List<JsonSchema>();
        foreach (var schemaDef in _contentPartDefinitionSchemaDefinitions.Where(x => x.Type == ContentDefinitionSchemaDefinition.Part))
        {
            var settingsSchema = await schemaDef.GetSettingsSchemaAsync();
            allSettingsSchemas.Add(settingsSchema);
        }

        if (allSettingsSchemas.Count > 0)
        {
            partsSchemaBuilder = partsSchemaBuilder
                .Properties(
                    ("Settings", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .AllOf(allSettingsSchemas) // merge all known part settings
                        .AdditionalProperties(true) // allow extra unknown settings
                    )
                );
        }

        return partsSchemaBuilder;
    }

    private async ValueTask<JsonSchemaBuilder> GetFieldsSchemaBuilderAsync(IEnumerable<Type> fieldTypes)
    {
        // Base schema for a content part field
        var fieldSchemaBuilder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                // $.ContentParts[].ContentPartFieldDefinitionRecords[].FieldName
                ("FieldName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(fieldTypes.Select(field => field.Name))
                ),

                // $.ContentParts[].ContentPartFieldDefinitionRecords[].Name
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                // $.ContentParts[].ContentPartFieldDefinitionRecords[].Settings
                ("Settings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ContentPartFieldSettings", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .Properties(
                                ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("Position", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("DisplayMode", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("Editor", new JsonSchemaBuilder().Type(SchemaValueType.String))
                            )
                            .AdditionalProperties(false) // no extra keys under ContentPartFieldSettings
                        )
                    )
                    .AdditionalProperties(true)
                )
            )
            .Required("FieldName", "Name", "Settings")
            .AdditionalProperties(true);

        // Merge all dynamic field settings into Settings
        var allFieldSettingsSchemas = new List<JsonSchema>();
        foreach (var schemaDef in _contentPartDefinitionSchemaDefinitions.Where(x => x.Type == ContentDefinitionSchemaDefinition.Field))
        {
            var settingsSchema = await schemaDef.GetSettingsSchemaAsync();
            allFieldSettingsSchemas.Add(settingsSchema);
        }

        if (allFieldSettingsSchemas.Count > 0)
        {
            fieldSchemaBuilder = fieldSchemaBuilder
                .Properties(
                    ("Settings", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .AllOf(allFieldSettingsSchemas) // merge all known field settings
                        .AdditionalProperties(true) // allow extra unknown settings
                    )
                );
        }

        return fieldSchemaBuilder;
    }
}
