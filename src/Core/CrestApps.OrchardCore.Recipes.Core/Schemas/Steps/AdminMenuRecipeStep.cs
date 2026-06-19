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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AdminMenu")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                            ("MenuItems", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(contentItemSchema)
                                .Description("The list of menu item content items.")))
                        .Required("Id", "Name", "MenuItems")
                        .AdditionalProperties(true))))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();
    }
}
