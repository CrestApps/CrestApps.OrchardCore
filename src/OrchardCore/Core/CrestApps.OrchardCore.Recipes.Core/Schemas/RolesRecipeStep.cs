using Json.Schema;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "Roles" recipe step — creates or updates roles with permissions.
/// </summary>
public sealed class RolesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    private readonly IPermissionService _permissionService;

    public RolesRecipeStep(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public string Name => "Roles";

    public async ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= await CreateSchemaAsync();

        return _cached;
    }

    private async Task<JsonSchema> CreateSchemaAsync()
    {
        var permission = await _permissionService.GetPermissionsAsync();

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Roles")),
                ("Roles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Permissions", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                                .Enum(permission.Select(x => x.Name))),
                            ("PermissionBehavior", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Enum("Add", "Replace", "Remove")
                                .Description("How permissions are merged: Add (default), Replace, or Remove.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .MinItems(1)))
            .Required("name", "Roles")
            .AdditionalProperties(true);

        return builder.Build();
    }
}
