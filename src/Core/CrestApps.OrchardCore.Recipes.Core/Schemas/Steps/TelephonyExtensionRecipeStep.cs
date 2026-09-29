using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "TelephonyExtension" recipe step — imports internal extensions: stable, tenant-scoped numbers that ring
/// an on-platform user so one agent can call another without knowing the other's provider endpoint.
/// </summary>
public sealed class TelephonyExtensionRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "TelephonyExtension";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Extensions", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("extension")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Catalog name of the extension used for admin display and search. When empty, it is built from the number and the display name.")),
                            ("Number", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The dialed extension number, for example '1001'. It must be unique within the tenant.")),
                            ("UserName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("User name of the user the extension rings. It is how the user is found on import, because user identifiers differ between environments; an entry whose user does not exist is not imported.")),
                            ("UserId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the user the extension rings. Used to find the user only when 'UserName' is empty, and replaced on import by the identifier of the user that was found.")),
                            ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Name shown to a colleague who calls the extension. When empty, the user name is used.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("extension")))
                        .Required("Number")
                        .AdditionalProperties(true))
                    .Description("The internal extensions to create or update.")),
            ],
            ["Extensions"]);
}
