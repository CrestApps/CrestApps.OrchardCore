using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.AI.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.AI.Tools;

/// <summary>
/// Reads the results of a survey: per question, the counts of each choice, number statistics or a few written answers.
/// </summary>
public sealed class GetSurveyResultsTool : SurveysAIToolBase
{
    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "survey": { "type": "string", "description": "The survey's technical name (from list_surveys) or id." }
          },
          "required": ["survey"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => SurveysAIConstants.Tools.GetSurveyResults;

    /// <inheritdoc />
    public override string Description => "Gets the results of a survey's published version: the number of completed responses, and per question the count of each choice, number statistics or a few written answers. Anonymous surveys with too few responses show no results.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = services.GetRequiredService<SurveyResultsReader>();
        var survey = await reader.FindAsync(Read(arguments, "survey"), cancellationToken);
        var result = await reader.ReadAsync(user, survey, cancellationToken);

        return result as string ?? Json(result);
    }
}
