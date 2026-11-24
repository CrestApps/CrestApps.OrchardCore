using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Coursework.Recipes;

/// <summary>
/// Imports coursework submissions. People are matched by user name; a row whose learner is not on this site is reported
/// as a recipe error and skipped. Submissions keep their ids, so a second import updates them. The files themselves are
/// not in the step: a submission refers to files in the protected store of the site it came from.
/// </summary>
internal sealed class LearningSubmissionsRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SubmissionStore _store;
    private readonly LearnerUserLookup _userLookup;

    internal readonly IStringLocalizer S;

    public LearningSubmissionsRecipeStep(SubmissionStore store, LearnerUserLookup userLookup, IStringLocalizer<LearningSubmissionsRecipeStep> stringLocalizer)
        : base(CourseworkConstants.SubmissionsRecipeStep)
    {
        _store = store;
        _userLookup = userLookup;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Submissions"] is not JsonArray rows)
        {
            return;
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            var userName = row["UserName"]?.GetValue<string>();
            var userId = await _userLookup.FindUserIdAsync(userName);
            var label = row["ItemId"]?.GetValue<string>() ?? row["CourseworkId"]?.GetValue<string>();

            if (userId is null)
            {
                context.Errors.Add(S["The user '{0}' of submission {1} is not on this site; it was skipped.", userName, label]);

                continue;
            }

            Submission incoming;

            try
            {
                incoming = row.Deserialize<Submission>(JsonOptions);
            }
            catch (JsonException exception)
            {
                context.Errors.Add(S["Submission {0} could not be read: {1}", label, exception.Message]);

                continue;
            }

            if (string.IsNullOrWhiteSpace(incoming.CourseworkId) || string.IsNullOrWhiteSpace(incoming.EnrollmentId))
            {
                context.Errors.Add(S["Submission {0} has no coursework or no enrollment; it was skipped.", label]);

                continue;
            }

            incoming.UserId = userId;
            incoming.GraderId = await _userLookup.FindUserIdAsync(row["GraderUserName"]?.GetValue<string>()) ?? incoming.GraderId;
            incoming.ReturnedBy = await _userLookup.FindUserIdAsync(row["ReturnedByUserName"]?.GetValue<string>()) ?? incoming.ReturnedBy;

            if (row["History"] is JsonArray history)
            {
                var grades = history.OfType<JsonObject>().ToArray();

                for (var i = 0; i < incoming.History.Count && i < grades.Length; i++)
                {
                    incoming.History[i].GraderId = await _userLookup.FindUserIdAsync(grades[i]["GraderUserName"]?.GetValue<string>()) ?? incoming.History[i].GraderId;
                    incoming.History[i].ReplacedBy = await _userLookup.FindUserIdAsync(grades[i]["ReplacedByUserName"]?.GetValue<string>()) ?? incoming.History[i].ReplacedBy;
                }
            }

            var existing = string.IsNullOrEmpty(incoming.ItemId) ? null : await _store.FindByIdAsync(incoming.ItemId);

            if (existing is null)
            {
                await _store.CreateAsync(incoming);
            }
            else
            {
                // Copied onto the instance the session tracks; saving the deserialized copy would store a second document.
                foreach (var property in typeof(Submission).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                    {
                        property.SetValue(existing, property.GetValue(incoming));
                    }
                }

                await _store.UpdateAsync(existing);
            }
        }
    }
}

/// <summary>
/// Describes the LearningSubmissions recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LearningSubmissionsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => CourseworkConstants.SubmissionsRecipeStep;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(CourseworkConstants.SubmissionsRecipeStep).Description("Recipe step discriminator. Must be 'LearningSubmissions'.")),
                ("Submissions", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ItemId", Text("The submission id, a generated unique id. A submission with this id is updated.")),
                        ("UserName", Text("The user name of the learner. A row whose user is not on the site is reported and skipped.")),
                        ("EnrollmentId", Text("The enrollment the work was handed in under.")),
                        ("CourseId", Text("The content item id of the course.")),
                        ("CourseworkId", Text("The content item id of the coursework.")),
                        ("CourseworkTitle", Text("The title of the coursework when the work was handed in.")),
                        ("Attempt", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The attempt number, from one.")),
                        ("Current", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether it is the learner's latest submission.")),
                        ("Text", Text("The text the learner typed.")),
                        ("Files", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The files: FileId, FileName and Size. The files themselves stay in the protected store of the site they were handed in on.")),
                        ("SubmittedUtc", Text("When the work was handed in, in UTC.")),
                        ("DueUtc", Text("The due date of the coursework when the work was handed in, in UTC; the late flag and penalty are worked out from it.")),
                        ("Late", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether it came after the due date.")),
                        ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Submitted", "Graded", "Returned").Description("The state.")),
                        ("Score", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The grade as a percentage.")),
                        ("Points", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The points given.")),
                        ("MaxPoints", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The points the work was graded out of.")),
                        ("RubricId", Text("The content item id of the rubric it was graded against.")),
                        ("RubricChoices", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The level id chosen for each rubric criterion id.")),
                        ("Feedback", Text("The grader's feedback.")),
                        ("GraderUserName", Text("The user name of the grader.")),
                        ("GradedUtc", Text("When it was graded, in UTC.")),
                        ("ReturnedByUserName", Text("The user name of the person who returned it.")),
                        ("ReturnedUtc", Text("When it was returned, in UTC.")),
                        ("History", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The earlier grades: Score, Points, MaxPoints, RubricChoices, Feedback, GraderUserName, GradedUtc, ReplacedByUserName, ReplacedUtc, Reason.")))
                    .Required("UserName", "CourseworkId", "EnrollmentId")).Description("The submissions to create, or update by id.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);
}
