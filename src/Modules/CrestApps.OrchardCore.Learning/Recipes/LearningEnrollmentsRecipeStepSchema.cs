using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Recipes;

/// <summary>
/// Describes the LearningEnrollments recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LearningEnrollmentsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => LearningConstants.EnrollmentsRecipeStep;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(LearningConstants.EnrollmentsRecipeStep).Description("Recipe step discriminator. Must be 'LearningEnrollments'.")),
                ("Enrollments", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(Enrollment()).Description("The enrollments to create, or update by id, with their progress.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);

    private static JsonSchemaBuilder Time(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Format(Formats.DateTime).Description(description);

    private static JsonSchemaBuilder Enrollment()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", Text("The enrollment id, a generated unique id. An enrollment with this id is updated.")),
                ("UserName", Text("The user name of the learner. A row whose user is not on the site is reported and skipped.")),
                ("CourseId", Text("The content item id of the course.")),
                ("OfferingId", Text("The course offering, when the course has offerings.")),
                ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Pending", "Active", "Suspended", "Completed", "Expired", "Withdrawn").Description("The state.")),
                ("Number", Text("The enrollment number.")),
                ("CreatedUtc", Time("When the enrollment was created, in UTC.")),
                ("StartedUtc", Time("When the learner started, in UTC.")),
                ("AccessEndsUtc", Time("When access ends, in UTC.")),
                ("DueUtc", Time("When the course is due, in UTC.")),
                ("CompletedUtc", Time("When the course was completed, in UTC.")),
                ("ProgressPercent", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(100).Description("The share of required items completed.")),
                ("Cycle", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("The pass number.")),
                ("PreviousEnrollmentId", Text("The enrollment this pass follows.")),
                ("Grants", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The reasons the learner has access: Source, SourceId, GrantedUtc, AccessEndsUtc.")),
                ("History", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The changes of state: From, To, ChangedUtc, Actor or ActorUserName, Reason.")),
                ("Completion", new JsonSchemaBuilder().Type(SchemaValueType.Object, SchemaValueType.Null).Description("The completion snapshot of a completed enrollment.")),
                ("Progress", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The progress on each course item: Items with ContentItemId, State, times, score and attestation.")))
            .Required("UserName", "CourseId", "Status")
            .AdditionalProperties(true);
    }
}
