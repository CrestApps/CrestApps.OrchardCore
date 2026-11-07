using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.TenantHierarchy.Recipes;

/// <summary>
/// Describes the <c>TenantHierarchyAccessGrants</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AccessGrantsRecipeStepSchema : RecipeStepSchemaBase
{
    public override string Name => TenantHierarchyConstants.Recipes.AccessGrants;

    protected override JsonSchema CreateSchema()
    {
        var grant = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Who in the parent tenant may sign in to child tenants, and with which roles there. A grant for the same user or role and child tenant is updated.")
            .Properties(
                ("PrincipalType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("User", "Role").Description("Whether the grant is for a user or a role of the parent tenant.")),
                ("Principal", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The user's name or email, or the role's name. It must exist in the parent tenant.")),
                ("ChildEntryId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The registry entry id of the child tenant. Leave it and ChildSlug out for every child tenant.")),
                ("ChildSlug", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The slug of the child tenant, used when its entry id isn't known on the site.")),
                ("ChildRoles", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The roles the user or role gets in the child tenant.")))
            .Required("PrincipalType", "Principal", "ChildRoles")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Adds or updates who in the parent tenant may sign in to child tenants, and with which roles.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TenantHierarchyConstants.Recipes.AccessGrants).Description($"Recipe step discriminator. Must be '{TenantHierarchyConstants.Recipes.AccessGrants}'.")),
                (AccessGrantsStep.EntriesName, new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(grant).Description("The access grants.")))
            .Required("name", AccessGrantsStep.EntriesName)
            .AdditionalProperties(true)
            .Build();
    }
}
