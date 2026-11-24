using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Learning.AI.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.AI.Tools;

/// <summary>
/// Returns the learner's own enrollments with their progress and dates.
/// </summary>
public sealed class MyLearningProgressTool : LearningAIToolBase
{
    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "courseId": { "type": "string", "description": "Optional. The id of one course; omit it for every course of the learner." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => LearningAIConstants.Tools.MyProgress;

    /// <inheritdoc />
    public override string Description => "Lists the signed-in learner's own courses with the enrollment status, the share and number of required items completed, the due date, when access ends, and the course grade. Times are in the learner's time zone.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override Permission Permission => LearningPermissions.AccessLearning;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var rows = await services.GetRequiredService<LearnerLearningReader>().GetProgressAsync(user, Read(arguments, "courseId"), cancellationToken);

        if (rows.Count == 0)
        {
            return "The learner is not enrolled in any course.";
        }

        return Json(rows);
    }
}

/// <summary>
/// Returns the next lesson the learner can open.
/// </summary>
public sealed class MyNextLessonTool : LearningAIToolBase
{
    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "courseId": { "type": "string", "description": "Optional. The id of one course; omit it for the course the learner worked on last." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => LearningAIConstants.Tools.MyNextLesson;

    /// <inheritdoc />
    public override string Description => "Finds the next lesson the signed-in learner has not completed and may open now, in course outline order, with a link that opens it.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override Permission Permission => LearningPermissions.AccessLearning;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var next = await services.GetRequiredService<LearnerLearningReader>().GetNextLessonAsync(user, Read(arguments, "courseId"), cancellationToken);

        if (next is null)
        {
            return "There is no lesson the learner can open now: every lesson is completed, the next ones are not released yet, or the learner has no active course.";
        }

        return Json(next);
    }
}
