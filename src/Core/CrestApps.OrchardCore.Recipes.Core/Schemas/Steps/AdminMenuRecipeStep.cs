using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "AdminMenu" recipe step — creates or updates admin menus and their nodes.
/// </summary>
public sealed class AdminMenuRecipeStep : IRecipeStep
{
    private readonly IAdminMenuSchemaService _adminMenuSchemaService;

    private JsonSchema _cached;

    /// <inheritdoc />
    public string Name => "AdminMenu";

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminMenuRecipeStep"/> class.
    /// </summary>
    /// <param name="adminMenuSchemaService">The service that composes the admin menu node schemas.</param>
    public AdminMenuRecipeStep(IAdminMenuSchemaService adminMenuSchemaService)
    {
        _adminMenuSchemaService = adminMenuSchemaService;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= await CreateSchemaAsync(cancellationToken);

        return _cached;
    }

    private async ValueTask<JsonSchema> CreateSchemaAsync(CancellationToken cancellationToken)
    {
        var adminMenuSchema = await _adminMenuSchemaService.GetAdminMenuSchemaAsync(cancellationToken);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("AdminMenu")
                    .Description("Recipe step discriminator. Must be 'AdminMenu'.")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(adminMenuSchema)
                    .Description("The admin menus to create or update.")))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();
    }
}
