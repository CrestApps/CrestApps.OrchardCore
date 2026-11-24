using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Certifications.Recipes;

/// <summary>
/// Describes the LearningCreditTypes recipe step.
/// </summary>
internal sealed class LearningCreditTypesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => CertificationsConstants.CreditTypesRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(CertificationsConstants.CreditTypesRecipeStepName).Description("Recipe step discriminator. Must be 'LearningCreditTypes'.")),
                ("CreditTypes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("itemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The credit type id; courses and certification paths refer to it.")),
                        ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name, such as CPD hours; types are matched by id, then by name.")),
                        ("unit", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The unit shown after an amount, such as hours.")),
                        ("description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A description.")),
                        ("allowExternalLearning", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether people can record external learning of this type.")))
                    .Required("name")).Description("The credit types to create, or update by id or name.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
