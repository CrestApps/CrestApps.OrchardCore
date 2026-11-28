using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
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

        // Only the person's own attempts are read, by their user id; a response id from the model is never trusted.
        var queries = services.GetRequiredService<ISurveyResponseQueries>();
        var surveys = services.GetRequiredService<ISurveyManager>();
        var (_, responses) = await queries.ListAsync(new SurveyResponseQuery
        {
            UserId = userId,
            Take = SurveysConstants.PageSize,
        }, cancellationToken);

        foreach (var response in responses
            .Where(response => string.Equals(response.UserId, userId, StringComparison.Ordinal) && response.Status != SurveyResponseStatus.InProgress)
            .Where(response => sequence is null || response.AttemptNumber == sequence)
            .OrderByDescending(response => response.CompletedUtc ?? response.StartedUtc))
        {
            var survey = await surveys.FindByIdAsync(response.SurveyId, cancellationToken);

            if (survey is null || !string.Equals(survey.Kind, SurveyKinds.Quiz, StringComparison.Ordinal))
            {
                continue;
            }

            var ownerId = survey.Owner?.Id;

            if (quiz is not null
                && !string.Equals(ownerId, quiz, StringComparison.Ordinal)
                && !string.Equals(survey.ItemId, quiz, StringComparison.Ordinal)
                && !(survey.Title?.Contains(quiz, StringComparison.CurrentCultureIgnoreCase) ?? false))
            {
                continue;
            }

            var version = await services.GetRequiredService<ISurveyVersionCache>().GetAsync(response.SurveyVersionId, cancellationToken);
            var shows = services.GetRequiredService<ISurveyReviewPolicy>().GetAllowed(survey, version?.Definition, response, services.GetRequiredService<IClock>().UtcNow);

            return Json(QuizResultExplainer.Describe(response, version?.Definition, survey.Title, shows, services.GetRequiredService<ISurveyControlRegistry>()));
        }

        return "The learner has no submitted quiz attempt that matches.";
    }
}
