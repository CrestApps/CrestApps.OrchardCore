using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Certifications.Recipes;

/// <summary>
/// Describes the LearningCertificationPaths recipe step.
/// </summary>
internal sealed class LearningCertificationPathsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => CertificationsConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var requirement = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("id", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The requirement id within the path; generated when omitted.")),
                ("kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("CourseCompleted or AssessmentPassed; other modules may add kinds.")),
                ("targetId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The content item id of the course or the quiz.")),
                ("minimumGrade", new JsonSchemaBuilder().Type(SchemaValueType.Number, SchemaValueType.Null).Minimum(0).Maximum(100).Description("The lowest grade or percent that counts.")))
            .Required("kind", "targetId");

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(CertificationsConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'LearningCertificationPaths'.")),
                ("Paths", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("itemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The path id.")),
                        ("certificationId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Skills certification, by id.")),
                        ("certificationName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Skills certification, by name; wins over the id.")),
                        ("enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the path issues certificates.")),
                        ("join", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Integer).Description("All (0) or Any (1).")),
                        ("initialRequirements", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(requirement).Description("What earns the first certificate.")),
                        ("renewalRequirements", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(requirement).Description("What renews it; empty means the first-certificate requirements.")),
                        ("referenceDate", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Integer).Description("CompletionDate (0), ExpiryDate (1) or FixedDate (2).")),
                        ("fixedMonth", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(12).Description("The month of the fixed expiry day.")),
                        ("fixedDay", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(31).Description("The day of the fixed expiry day.")),
                        ("minimumIntervalMonths", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("The shortest first period, for the fixed date rule.")),
                        ("renewalWindowDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("How many days before expiry renewal opens.")),
                        ("gracePeriodDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("How many days after expiry a renewal still counts.")),
                        ("startsOnFirstRequirement", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether meeting a requirement starts the path.")),
                        ("enrollOnRenewal", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether renewal enrolls the holder in the renewal courses.")))
                    ).Description("The paths to create, or update by certification.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
