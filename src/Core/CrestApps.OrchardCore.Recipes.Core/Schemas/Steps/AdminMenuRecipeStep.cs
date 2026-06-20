using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "AdminMenu" recipe step — creates or updates admin menu structures.
/// </summary>
public sealed class AdminMenuRecipeStep : IRecipeStep
{
    private readonly IContentItemSchemaService _contentItemSchemaService;

    private JsonSchema _cached;

    public string Name => "AdminMenu";

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminMenuRecipeStep"/> class.
    /// </summary>
    /// <param name="contentItemSchemaService">The content item schema service.</param>
    public AdminMenuRecipeStep(IContentItemSchemaService contentItemSchemaService)
    {
        _contentItemSchemaService = contentItemSchemaService;
    }

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= await CreateSchemaAsync(cancellationToken);

        return _cached;
    }

    private async ValueTask<JsonSchema> CreateSchemaAsync(CancellationToken cancellationToken)
    {
        var contentItemSchema = await _contentItemSchemaService.GetGenericSchemaAsync(cancellationToken: cancellationToken);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AdminMenu").Description("Recipe step discriminator. Must be 'AdminMenu'.")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique admin menu content item identifier.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Technical admin menu name.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the admin menu is enabled.")),
                            ("MenuItems", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(contentItemSchema)
                                .Description("The list of menu item content items.")))
                        .Required("Id", "Name", "MenuItems")
                        .AdditionalProperties(true))
                    .Description("Admin menus to create or update.")))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();
    }
}
