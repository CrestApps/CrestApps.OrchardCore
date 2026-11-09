using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the <c>ReloadTenant</c> recipe step, which releases the tenant when the recipe completes so that
/// settings cached at startup (for example CORS policies) take effect without a manual restart.
/// </summary>
public sealed class ReloadTenantRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc />
    public override string Name => "ReloadTenant";

    protected override JsonSchema CreateSchema()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Requests a reload of the tenant when the recipe completes. Place it after the steps whose settings must become active, such as a 'Settings' step that changes CORS policies.")
            .Properties(
                ("name", RecipeStepSchemaBuilders.String()
                    .Const(Name)
                    .Description($"Recipe step discriminator. Must be '{Name}'.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
    }
}
