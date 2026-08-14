using Json.Schema;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Roles" recipe step — creates or updates roles with permissions.
/// </summary>
public sealed class RolesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    private readonly IPermissionService _permissionService;
    private readonly IRoleService _roleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesRecipeStep"/> class.
    /// </summary>
    /// <param name="permissionService">The permission service.</param>
    /// <param name="roleService">The role service.</param>
    public RolesRecipeStep(
        IPermissionService permissionService,
        IRoleService roleService)
    {
        _permissionService = permissionService;
        _roleService = roleService;
    }

    public string Name => "Roles";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= await CreateSchemaAsync();

        return _cached;
    }

    private async Task<JsonSchema> CreateSchemaAsync()
    {
        var permissionNames = (await _permissionService.GetPermissionsAsync())
            .Select(permission => permission?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var roleNames = (await _roleService.GetRolesAsync())
            .Select(role => role?.RoleName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Roles").Description("Recipe step discriminator. Must be 'Roles'.")),
                ("Roles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", RecipeStepSchemaBuilders.SuggestedString(roleNames)
                                .Description("Role name. Existing roles are surfaced as suggestions, but any role name is allowed, including new roles to create.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Administrative description of the role.")),
                            ("Permissions", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(RecipeStepSchemaBuilders.SuggestedString(permissionNames))
                                .Description("Permissions assigned to the role. Well-known permissions are surfaced as suggestions, but any valid permission name is allowed, including dynamic per-content-type permissions (for example, 'EditContent_Article') and permissions provided by third-party modules.")),
                            ("PermissionBehavior", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Enum("Add", "Replace", "Remove")
                                .Description("How permissions are merged: Add (default), Replace, or Remove.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .MinItems(1)
                    .Description("Roles to create or update.")))
            .Required("name", "Roles")
            .AdditionalProperties(true);

        return builder.Build();
    }
}
