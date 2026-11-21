using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Assessments.Recipes;

/// <summary>
/// Describes the LearningAttempts recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LearningAttemptsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => AssessmentsConstants.AttemptsRecipeStep;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(AssessmentsConstants.AttemptsRecipeStep).Description("Recipe step discriminator. Must be 'LearningAttempts'.")),
                ("Attempts", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ItemId", Text("The attempt id, a generated unique id. An attempt with this id is updated.")),
                        ("UserName", Text("The user name of the learner. A row whose user is not on the site is reported and skipped.")),
                        ("QuizId", Text("The content item id of the quiz.")),
                        ("CourseId", Text("The content item id of the course.")),
                        ("EnrollmentId", Text("The enrollment the attempt belongs to.")),
                        ("Number", Text("The attempt number.")),
                        ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("InProgress", "NeedsGrading", "Graded", "Voided", "Abandoned").Description("The state.")),
                        ("Items", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The questions as drawn, with their answers, keys and scores: the attempt is a self-contained record.")),
                        ("ScoreHistory", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The score changes: ChangedUtc, Kind, From, To, Actor or ActorUserName, Reason.")),
                        ("GradedByUserName", Text("The user name of the grader.")))
                    .Required("UserName", "QuizId")).Description("The attempts to create, or update by id.")),
                ("Overrides", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ItemId", Text("The override id. An override with this id is updated.")),
                        ("UserName", Text("The user name of the learner.")),
                        ("QuizId", Text("The content item id of the quiz.")),
                        ("ExtraMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Extra minutes on each attempt.")),
                        ("ExtraAttempts", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Extra attempts.")),
                        ("OpensUtc", Text("When the quiz opens for this learner, in UTC.")),
                        ("ClosesUtc", Text("When the quiz closes for this learner, in UTC.")),
                        ("Reason", Text("Why the override was given.")),
                        ("ActorUserName", Text("The user name of the person who gave it.")))
                    .Required("UserName", "QuizId")).Description("The quiz overrides to create, or update by id.")),
                ("AccessCodes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("QuizId", Text("The content item id of the quiz.")),
                        ("Hash", Text("The salted hash of the access code, as exported. The code itself is never exported.")))
                    .Required("QuizId", "Hash")).Description("The access codes of quizzes, as hashes.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);
}
