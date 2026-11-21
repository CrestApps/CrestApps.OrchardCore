using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.AI.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.AI.Tools;

/// <summary>
/// Drafts a new survey from instructions or a document. The draft is saved for the author to review and is never
/// published by the tool.
/// </summary>
public sealed class CreateSurveyDraftTool : SurveysAIToolBase
{
    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Optional. The survey's title." },
            "instructions": { "type": "string", "description": "What the survey is for and what it should ask, or the text of a document to turn into a survey." },
            "kind": { "type": "string", "enum": ["Survey", "Quiz", "Poll"], "description": "Optional. The kind of survey; Survey by default." }
          },
          "required": ["instructions"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => SurveysAIConstants.Tools.CreateSurveyDraft;

    /// <inheritdoc />
    public override string Description => "Drafts a new survey, poll or quiz from instructions or a document and saves it as a draft for the user to review in the survey designer. It never publishes the survey; tell the user to review and publish it.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var result = await services.GetRequiredService<SurveyDraftAssistant>().DraftAsync(user, Read(arguments, "title"), Read(arguments, "instructions"), Read(arguments, "kind"), cancellationToken);

        if (result.Error is not null)
        {
            return result.Error;
        }

        return Json(new
        {
            name = result.Survey.Name,
            title = result.Survey.Title,
            status = result.Survey.Status.ToString(),
            designer = $"~/Admin/surveys/{result.Survey.ItemId}/design",
            problems = result.Messages.Select(message => new { error = message.IsError, message = message.Message }),
        });
    }
}
