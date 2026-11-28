using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.AI.Tools;

/// <summary>
/// Lists the surveys the user manages or may read the results of.
/// </summary>
public sealed class ListSurveysTool : SurveysAIToolBase
{
    private const int MaxResults = 25;

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "search": { "type": "string", "description": "Optional. Words of the survey's title or name." },
            "status": { "type": "string", "enum": ["Draft", "Open", "Closed", "Archived"], "description": "Optional. Only surveys in this status." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => SurveysAIConstants.Tools.ListSurveys;

    /// <inheritdoc />
    public override string Description => "Lists the surveys, polls and quizzes the user manages or may read the results of: their technical name, title, kind, status, published version and number of responses (none for an anonymous survey with fewer responses than its minimum group size).";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var list = services.GetRequiredService<SurveyListService>();
        var authorization = services.GetRequiredService<ISurveyAuthorizationService>();
        var responses = services.GetRequiredService<ISurveyResponseQueries>();
        var groups = services.GetRequiredService<SurveyAnonymousGroups>();
        var authorizationService = services.GetRequiredService<IAuthorizationService>();

        // A user who may only manage or read their own surveys reads their own, as the surveys list does, so their older
        // surveys aren't lost behind the most recent surveys of everyone else.
        var seesAll = await authorizationService.AuthorizeAsync(user, SurveysPermissions.ManageSurveys)
            || await authorizationService.AuthorizeAsync(user, SurveysPermissions.ViewSurveyResponses);
        var (_, surveys) = await list.ListAsync(new SurveyListQuery
        {
            Search = Read(arguments, "search"),
            Status = Read(arguments, "status"),
            OwnerId = seesAll ? null : user.FindFirstValue(ClaimTypes.NameIdentifier),
        }, 0, 200, cancellationToken);
        var rows = new List<object>();

        foreach (var survey in surveys)
        {
            if (rows.Count >= MaxResults)
            {
                break;
            }

            if (!await authorization.AuthorizeAsync(user, survey, SurveyOperation.Manage, cancellationToken) &&
                !await authorization.AuthorizeAsync(user, survey, SurveyOperation.ViewResponses, cancellationToken))
            {
                continue;
            }

            // An anonymous survey's count follows the responses list's small-group rule: none until enough people
            // completed it, and no status too few people share, so the count can't tell when one person answered.
            long? count = SurveyAnonymity.IsAnonymous(survey)
                ? await groups.CountShownAsync(survey, cancellationToken)
                : await responses.CountForSurveyAsync(survey.ItemId, cancellationToken);

            rows.Add(new
            {
                name = survey.Name,
                title = survey.Title,
                kind = survey.Kind,
                status = survey.Status.ToString(),
                publishedVersion = survey.PublishedVersionNumber,
                responses = count,
                responsesHidden = count is null ? "Anonymous survey with fewer responses than its minimum group size." : null,
            });
        }

        return rows.Count == 0 ? "The user has no survey they manage or may read the results of." : Json(rows);
    }
}
