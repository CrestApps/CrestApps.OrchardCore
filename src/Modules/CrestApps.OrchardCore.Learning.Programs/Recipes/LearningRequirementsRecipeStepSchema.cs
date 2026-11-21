using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Programs.Recipes;

/// <summary>
/// Describes the LearningRequirements recipe step.
/// </summary>
internal sealed class LearningRequirementsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ProgramsConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(ProgramsConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'LearningRequirements'.")),
                ("Requirements", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("itemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The rule id.")),
                        ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The rule name; rules are matched by id, then by name.")),
                        ("enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the rule is applied.")),
                        ("targetKind", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Course, LearningPath or CertificationPath.")),
                        ("targetId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The content item id of the course or path, or the certification path id.")),
                        ("audienceName", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The technical name of the user audience.")),
                        ("userIds", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("User ids required besides the audience.")),
                        ("dueRule", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Integer).Description("None (0), FixedDate (1), DaysAfterRequired (2) or DaysAfterHire (3).")),
                        ("dueDate", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The due day, yyyy-MM-dd, for FixedDate.")),
                        ("dueDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("The days for the rules that count days.")),
                        ("reminderDays", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1)).Description("Days before the due date to remind the person.")),
                        ("escalateAfterDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer, SchemaValueType.Null).Minimum(0).Description("Days after the due date to tell the manager; null never.")))
                    .Required("name", "targetKind", "targetId")).Description("The rules to create, or update by id or name.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
