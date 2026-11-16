using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.AI.Tools;

/// <summary>
/// Explains the learner's own quiz result as far as the quiz's review policy allows.
/// </summary>
public sealed class ExplainMyQuizResultTool : LearningAIToolBase
{
    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "quiz": { "type": "string", "description": "Optional. The quiz's id or part of its title; omit it for the learner's latest quiz." },
            "attempt": { "type": "integer", "description": "Optional. Which attempt of that quiz, counting from 1; omit it for the latest." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => LearningAIConstants.Tools.ExplainMyResult;

    /// <inheritdoc />
    public override string Description => "Gets the signed-in learner's own result of a quiz to explain it: the score when the quiz shows it, and the questions, the learner's answers, the right answers and the feedback only when the quiz's review policy lets the learner review the attempt. When it does not, say so and never guess the answers.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override Permission Permission => LearningPermissions.AccessLearning;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var quiz = Read(arguments, "quiz");
        var sequence = ReadInt(arguments, "attempt");

        // Only the person's own attempts are read, by their user id; an attempt id from the model is never trusted.
        var attempts = (await services.GetRequiredService<AssessmentAttemptStore>().ListByUserAsync(userId, cancellationToken))
            .Where(attempt => string.Equals(attempt.UserId, userId, StringComparison.Ordinal))
            .Where(attempt => attempt.Status != AttemptStatus.InProgress)
            .Where(attempt => quiz is null
                || string.Equals(attempt.QuizId, quiz, StringComparison.Ordinal)
                || (attempt.QuizTitle?.Contains(quiz, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .Where(attempt => sequence is null || attempt.Sequence == sequence)
            .OrderByDescending(attempt => attempt.SubmittedUtc ?? attempt.StartedUtc)
            .ToList();

        var chosen = attempts.FirstOrDefault();

        if (chosen is null)
        {
            return "The learner has no submitted quiz attempt that matches.";
        }

        var policy = services.GetRequiredService<AttemptReviewPolicy>();

        return Json(QuizResultExplainer.Describe(chosen, policy.CanReview(chosen), AttemptReviewPolicy.CanSeeScore(chosen)));
    }
}
