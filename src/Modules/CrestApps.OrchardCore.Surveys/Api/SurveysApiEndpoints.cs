using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.Api;

/// <summary>
/// The Surveys HTTP API under <c>/api/surveys</c>, on the Api authentication scheme. Respondent calls act as the
/// signed-in client and go through the same access rules, logic and scoring as the runner; staff calls check the survey
/// permissions. Answer keys never leave the server.
/// </summary>
internal static class SurveysApiEndpoints
{
    public const string RoutePrefix = "api/surveys";

    public static IEndpointRouteBuilder MapSurveysApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api })
            .DisableAntiforgery()
            .WithTags("Surveys");

        group.MapGet(string.Empty, ListSurveysAsync).WithName("SurveysApiList").WithSummary("Lists the surveys the caller may manage.");
        group.MapGet("{id}/definition", GetDefinitionAsync).WithName("SurveysApiDefinition").WithSummary("Gets what a respondent sees of a survey's published version, without answer keys.");
        group.MapGet("{id}/responses", ListResponsesAsync).WithName("SurveysApiResponses").WithSummary("Lists a page of a survey's responses.");
        group.MapPost("responses", StartAsync).WithName("SurveysApiStart").WithSummary("Starts, or resumes, the caller's response to a survey.");
        group.MapGet("responses/{responseId}", GetPageAsync).WithName("SurveysApiPage").WithSummary("Gets the current page of the caller's response.");
        group.MapPut("responses/{responseId}/pages/{pageId}", SubmitPageAsync).WithName("SurveysApiSubmitPage").WithSummary("Answers the current page and moves to the next one (or back).");
        group.MapPost("responses/{responseId}/complete", CompleteAsync).WithName("SurveysApiComplete").WithSummary("Answers the last page and completes the response.");

        return routes;
    }

    internal static async Task<IResult> ListSurveysAsync(HttpContext httpContext, IAuthorizationService authorizationService, ISurveyAuthorizationService surveyAuthorization, ISession session, SurveyStore surveyStore)
    {
        var user = httpContext.User;

        if (!await authorizationService.AuthorizeAsync(user, SurveysPermissions.ManageOwnSurveys))
        {
            return Results.Forbid();
        }

        var rows = await session.QueryIndex<SurveyIndex>(index => index.Status != nameof(SurveyStatus.Archived), collection: SurveysConstants.CollectionName)
            .OrderBy(index => index.Title)
            .Take(SurveysConstants.PageSize)
            .ListAsync(httpContext.RequestAborted);

        var surveys = await surveyStore.GetAsync(rows.Select(row => row.ItemId), httpContext.RequestAborted);
        var result = new List<object>();

        foreach (var survey in surveys)
        {
            if (await surveyAuthorization.AuthorizeAsync(user, survey, SurveyOperation.Manage, httpContext.RequestAborted))
            {
                result.Add(new
                {
                    id = survey.ItemId,
                    name = survey.Name,
                    title = survey.Title,
                    kind = survey.Kind,
                    status = survey.Status.ToString(),
                    publishedVersion = survey.PublishedVersionNumber,
                });
            }
        }

        return Results.Ok(result);
    }

    internal static async Task<IResult> GetDefinitionAsync(string id, string collector, HttpContext httpContext, IAuthorizationService authorizationService, SurveyStore surveyStore, SurveyCollectorStore collectorStore, ISurveyVersionCache versionCache, SurveyRespondentProjector projector)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SurveysPermissions.RespondToSurveys))
        {
            return Results.Forbid();
        }

        var survey = await surveyStore.FindByIdAsync(id, httpContext.RequestAborted);

        if (survey is null || survey.Status != SurveyStatus.Open)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrEmpty(collector))
        {
            var found = await collectorStore.FindByTokenAsync(collector, httpContext.RequestAborted);

            if (found is null || found.SurveyId != survey.ItemId)
            {
                return Results.NotFound();
            }
        }

        var version = await versionCache.GetPublishedAsync(survey, httpContext.RequestAborted);

        if (version?.Definition is null)
        {
            return Results.NotFound();
        }

        var definition = version.Definition;

        return Results.Ok(new
        {
            id = survey.ItemId,
            version = version.Number,
            title = string.IsNullOrWhiteSpace(definition.Title) ? survey.Title : definition.Title,
            introduction = definition.Introduction,
            pages = definition.Pages.Select(page => new
            {
                id = page.Id,
                name = page.Name,
                title = page.Title,
                description = page.Description,
                questions = (page.Questions ?? []).Select(question => projector.Project(question, definition, null, null, null)),
            }),
        });
    }

    internal static async Task<IResult> ListResponsesAsync(string id, int? skip, int? take, HttpContext httpContext, IAuthorizationService authorizationService, ISurveyAuthorizationService surveyAuthorization, SurveyStore surveyStore, ISurveyResponseQueries queries)
    {
        var survey = await surveyStore.FindByIdAsync(id, httpContext.RequestAborted);

        if (survey is null)
        {
            return Results.NotFound();
        }

        if (!await surveyAuthorization.AuthorizeAsync(httpContext.User, survey, SurveyOperation.ViewResponses, httpContext.RequestAborted))
        {
            return Results.Forbid();
        }

        var withIdentity = survey.Access?.Anonymity != AnonymityMode.Anonymous
            && await authorizationService.AuthorizeAsync(httpContext.User, SurveysPermissions.ExportSurveyResponses);
        var (count, items) = await queries.ListAsync(new SurveyResponseQuery
        {
            SurveyIds = [survey.ItemId],
            Skip = Math.Max(skip ?? 0, 0),
            Take = Math.Clamp(take ?? 50, 1, SurveysConstants.PageSize),
        }, httpContext.RequestAborted);

        return Results.Ok(new
        {
            count,
            items = items.Select(response => new
            {
                id = response.ItemId,
                number = response.Number,
                version = response.VersionNumber,
                status = response.Status.ToString(),
                grading = response.Grading.ToString(),
                userId = withIdentity ? response.UserId : null,
                respondent = withIdentity ? response.Respondent?.ToString() : null,
                context = response.Context?.ToString(),
                startedUtc = DateTime.SpecifyKind(response.StartedUtc, DateTimeKind.Utc),
                completedUtc = response.CompletedUtc is { } completed ? DateTime.SpecifyKind(completed, DateTimeKind.Utc) : (DateTime?)null,
                score = response.Score,
                maxScore = response.MaxScore,
                percent = response.Percent,
                passed = response.Passed,
            }),
        });
    }

    internal static async Task<IResult> StartAsync(StartResponseInput input, HttpContext httpContext, IAuthorizationService authorizationService, ISurveyResponseService responses)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SurveysPermissions.RespondToSurveys))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(input?.SurveyId) && string.IsNullOrWhiteSpace(input?.CollectorToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["surveyId"] = ["Give the survey id or a collector token."] });
        }

        RecordRef? context = null;

        if (!string.IsNullOrWhiteSpace(input.Context))
        {
            if (!RecordRef.TryParse(input.Context, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["context"] = ["Write the context as Kind:Id."] });
            }

            context = parsed;
        }

        var result = await responses.StartAsync(new StartResponseRequest
        {
            SurveyId = input.SurveyId,
            CollectorToken = input.CollectorToken,
            Identity = new SurveyRespondentIdentity { UserId = UserId(httpContext.User) },
            Context = context,
            Variables = input.Variables ?? [],
            Culture = input.Culture,
        }, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return Problem(result.Error);
        }

        var page = await responses.GetPageAsync(result.Response, httpContext.RequestAborted);

        return Results.Ok(PageResult(page, result.Resumed));
    }

    internal static async Task<IResult> GetPageAsync(string responseId, HttpContext httpContext, IAuthorizationService authorizationService, ISurveyResponseService responses)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SurveysPermissions.RespondToSurveys))
        {
            return Results.Forbid();
        }

        var response = await responses.FindForRespondentAsync(responseId, new SurveyRespondentIdentity { UserId = UserId(httpContext.User) }, httpContext.RequestAborted);

        if (response is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(PageResult(await responses.GetPageAsync(response, httpContext.RequestAborted), resumed: true));
    }

    internal static Task<IResult> SubmitPageAsync(string responseId, string pageId, SubmitPageInput input, HttpContext httpContext, IAuthorizationService authorizationService, ISurveyResponseService responses)
        => PostAsync(responseId, pageId, input, string.Equals(input?.Direction, "back", StringComparison.OrdinalIgnoreCase) ? SurveyPageDirection.Back : SurveyPageDirection.Next, httpContext, authorizationService, responses);

    internal static Task<IResult> CompleteAsync(string responseId, SubmitPageInput input, HttpContext httpContext, IAuthorizationService authorizationService, ISurveyResponseService responses)
        => PostAsync(responseId, null, input, SurveyPageDirection.Finish, httpContext, authorizationService, responses);

    private static async Task<IResult> PostAsync(string responseId, string pageId, SubmitPageInput input, SurveyPageDirection direction, HttpContext httpContext, IAuthorizationService authorizationService, ISurveyResponseService responses)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SurveysPermissions.RespondToSurveys))
        {
            return Results.Forbid();
        }

        var result = await responses.SubmitPageAsync(new SubmitPageRequest
        {
            ResponseId = responseId,
            PageId = pageId,
            Direction = direction,
            Identity = new SurveyRespondentIdentity { UserId = UserId(httpContext.User) },
            Answers = input?.Answers ?? [],
        }, httpContext.RequestAborted);

        if (result.Error == SurveyErrorCode.InvalidAnswer)
        {
            return Results.ValidationProblem(result.Errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        }

        return result.Succeeded ? Results.Ok(PageResult(result, resumed: false)) : Problem(result.Error);
    }

    private static object PageResult(SurveyPageResult result, bool resumed)
        => new
        {
            responseId = result.Response?.ItemId,
            number = result.Response?.Number,
            status = result.Response?.Status.ToString(),
            resumed,
            ended = result.Ended || result.Response?.Status is not (null or SurveyResponseStatus.InProgress),
            page = result.Page is null
                ? null
                : new
                {
                    id = result.Page.Id,
                    title = result.Page.Title,
                    description = result.Page.Description,
                    position = result.Page.Position,
                    pageCount = result.Page.PageCount,
                    isLast = result.Page.IsLast,
                    canGoBack = result.Page.CanGoBack,
                    deadlineUtc = result.Page.DeadlineUtc,
                    questions = result.Page.Questions.Where(question => !result.Page.HiddenQuestionIds.Contains(question.Id)),
                },
        };

    private static IResult Problem(SurveyErrorCode error)
    {
        var status = error switch
        {
            SurveyErrorCode.NotFound => StatusCodes.Status404NotFound,
            SurveyErrorCode.NotOwner or SurveyErrorCode.NotInAudience or SurveyErrorCode.SignInRequired => StatusCodes.Status403Forbidden,
            SurveyErrorCode.Busy or SurveyErrorCode.AlreadyInProgress or SurveyErrorCode.InvalidState => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        return Results.Problem(statusCode: status, title: error.ToString(), type: "https://crestapps.com/surveys/errors/" + error.ToString().ToLowerInvariant());
    }

    private static string UserId(ClaimsPrincipal user)
        => user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
}
