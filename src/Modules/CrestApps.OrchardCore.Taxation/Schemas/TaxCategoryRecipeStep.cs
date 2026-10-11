using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Schemas;

/// <summary>
/// Schema for the "TaxCategory" recipe step — creates or updates tax categories.
/// </summary>
public sealed class TaxCategoryRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    /// <inheritdoc />
    public string Name => TaxationDeploymentSteps.TaxCategory;

    /// <inheritdoc />
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var itemSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The stable identifier of the category. Preserved across environments.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The display name of the category.")),
                ("Code", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The unique code of the category (for example 'Electronics').")),
                ("ParentCode", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The code of the parent category, forming a hierarchy.")),
                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A description of the category.")),
                ("ExternalCodes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Provider-specific tax codes keyed by provider name.")))
            .Required("Name", "Code")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TaxationDeploymentSteps.TaxCategory).Description($"Recipe step discriminator. Must be '{TaxationDeploymentSteps.TaxCategory}'.")),
                ("TaxCategories", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(itemSchema)
                    .MinItems(1)
                    .Description("The tax categories to create or update.")))
            .Required("name", "TaxCategories")
            .AdditionalProperties(true)
            .Build();
    }
}
