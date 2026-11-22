using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.Settings;
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
            .WithRateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)
            .AddEndpointFilter(NoStoreAndUnreadableSecretFilterAsync)
            .WithTags("Surveys");

        group.MapGet(string.Empty, ListSurveysAsync).WithName("SurveysApiList").WithSummary("Lists the surveys the caller may manage.");
        group.MapGet("{id}/definition", GetDefinitionAsync).WithName("SurveysApiDefinition").WithSummary("Gets what a respondent sees of a survey: the published version after the access rules, or the version of the caller's started response (responseId). No answer keys, no pooled questions without a response, and choices whose order is a key are shuffled.");
        group.MapGet("{id}/responses", ListResponsesAsync).WithName("SurveysApiResponses").WithSummary("Lists a page of a survey's responses.");
        group.MapPost("responses", StartAsync).WithName("SurveysApiStart").WithSummary("Starts, or resumes, the caller's response to a survey.");
        group.MapGet("responses/{responseId}", GetPageAsync).WithName("SurveysApiPage").WithSummary("Gets the current page of the caller's response.");
        group.MapPut("responses/{responseId}/pages/{pageId}", SubmitPageAsync).WithName("SurveysApiSubmitPage").WithSummary("Answers the current page and moves to the next one (or back).");
        group.MapPost("responses/{responseId}/complete", CompleteAsync).WithName("SurveysApiComplete").WithSummary("Answers the last page and completes the response.");

        return routes;
    }

    /// <summary>
    /// Runs around every Surveys API call. The answers, pages and responses it returns are never cached by a browser or a
    /// shared proxy (<c>Cache-Control: no-store</c>), and an anonymous survey whose secret this server can't read (Data
    /// Protection keys that aren't shared) answers 503 with a problem result instead of an unhandled error.
    /// </summary>
    /// <param name="context">The call.</param>
    /// <param name="next">The endpoint.</param>
    /// <returns>The endpoint's result, or the 503 problem result.</returns>
    internal static async ValueTask<object> NoStoreAndUnreadableSecretFilterAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var headers = context.HttpContext.Response.Headers;

        headers.CacheControl = "no-store";
        headers.Pragma = "no-cache";

        try
        {
            return await next(context);
        }
        catch (SurveyAnonymitySecretUnreadableException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "SurveyUnavailable",
                detail: "This survey isn't available right now. Please try again later.",
                type: "https://crestapps.com/surveys/errors/surveyunavailable");
        }
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

    internal static async Task<IResult> GetDefinitionAsync(
        string id,
        string collector,
        string responseId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        ISurveyVersionCache versionCache,
        ISurveyResponseService responses,
        SurveyRuntimeVersions runtimeVersions,
        SurveyAccessEvaluator access,
        SurveyPlanBuilder planBuilder,
        SurveyRespondentProjector projector,
        IClock clock)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SurveysPermissions.RespondToSurveys))
        {
            return Results.Forbid();
        }

        var survey = await surveyStore.FindByIdAsync(id, httpContext.RequestAborted);

        if (survey is null)
        {
            return Results.NotFound();
        }

        var identity = new SurveyRespondentIdentity { UserId = UserId(httpContext.User) };
        SurveyVersion version;
        SurveyResponsePlan plan;
        var fromResponse = !string.IsNullOrWhiteSpace(responseId);

        if (fromResponse)
        {
            // The definition of the caller's own started response: its version, and only the questions its plan drew.
            var response = await responses.FindForRespondentAsync(responseId, identity, httpContext.RequestAborted);

            if (response is null || !string.Equals(response.SurveyId, survey.ItemId, StringComparison.Ordinal) || response.Status != SurveyResponseStatus.InProgress)
            {
                return Results.NotFound();
            }

            version = await runtimeVersions.GetAsync(survey, response, httpContext.RequestAborted);
            plan = response.Plan;
        }
        else
        {
            if (survey.Status != SurveyStatus.Open)
            {
                return Results.NotFound();
            }

            SurveyCollector found;

            if (!string.IsNullOrEmpty(collector))
            {
                found = await collectorStore.FindByTokenAsync(collector, httpContext.RequestAborted);

                if (found is null || found.SurveyId != survey.ItemId)
                {
                    return Results.NotFound();
                }
            }
            else
            {
                found = await collectorStore.FindDefaultAsync(survey.ItemId, httpContext.RequestAborted);
            }

            version = await versionCache.GetPublishedAsync(survey, httpContext.RequestAborted);

            if (version?.Definition is null)
            {
                return Results.NotFound();
            }

            // The same access rules as starting a response (audience, password, invitation, enrollment...), attempt
            // limits included: seeing the questions is not an attempt, but a respondent with no attempt left must not
            // read a quiz they can no longer take.
            var overrides = httpContext.RequestServices?.GetService<ISurveyOverrideService>();
            var secrets = httpContext.RequestServices?.GetService<SurveyAnonymitySecrets>();
            var decision = await access.CheckStartAsync(new SurveyAccessContext
            {
                Survey = survey,
                Version = version,
                Collector = found,
                Identity = identity,
                RespondentKey = secrets is null ? null : await secrets.RespondentKeyAsync(survey, identity, null, httpContext.RequestAborted),
                Override = overrides is null ? null : await overrides.FindEffectiveAsync(survey.ItemId, identity.UserId, null, httpContext.RequestAborted),
                UtcNow = clock.UtcNow,
            });

            if (!decision.IsAllowed)
            {
                return Problem(decision.Error);
            }

            // A plan made for this call only: it shuffles the choices of types whose key is an order, as the runner does.
            plan = planBuilder.Build(survey.ItemId, version.Definition);
        }

        if (version?.Definition is null)
        {
            return Results.NotFound();
        }

        var definition = version.Definition;
        var sanitizer = httpContext.RequestServices?.GetService<SurveyContentSanitizer>();
        var drawn = (plan?.QuestionOrder ?? []).Values.SelectMany(ids => ids ?? []).ToHashSet(StringComparer.Ordinal);

        return Results.Ok(new
        {
            id = survey.ItemId,
            version = version.Number,
            title = string.IsNullOrWhiteSpace(definition.Title) ? survey.Title : definition.Title,
            introduction = Html(sanitizer, definition.Introduction),
            pages = (definition.Pages ?? []).Where(page => page is not null).Select(page => new
            {
                id = page.Id,
                name = page.Name,
                title = page.Title,
                description = Html(sanitizer, page.Description),

                // Without a started response, pooled questions are left out: a fresh draw on every call would show the
                // whole pool.
                questions = (page.Questions ?? [])
                    .Where(question => question is not null && (fromResponse ? drawn.Contains(question.Id) : !IsPooled(page, question)))
                    .Select(question => Sanitized(sanitizer, projector.Project(question, definition, plan, null, null)))
                    .ToList(),
            }).ToList(),
        });
    }

    internal static async Task<IResult> ListResponsesAsync(string id, int? skip, int? take, HttpContext httpContext, ISurveyAuthorizationService surveyAuthorization, SurveyStore surveyStore, ISurveyResponseQueries queries)
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

        // The one identity rule: who responded shows on an identified survey to whoever may read its responses. An
        // anonymous survey lists nothing below its minimum group size, and shows days only and context kinds only.
        var services = httpContext.RequestServices;
        var withIdentity = await services.GetRequiredService<SurveyRespondentNames>().CanSeeIdentityAsync(httpContext.User, survey, httpContext.RequestAborted);
        var anonymous = SurveyAnonymity.IsAnonymous(survey);
        var (count, items) = await queries.ListAsync(new SurveyResponseQuery
        {
            SurveyIds = [survey.ItemId],
            Skip = Math.Max(skip ?? 0, 0),
            Take = Math.Clamp(take ?? 50, 1, SurveysConstants.PageSize),
        }, httpContext.RequestAborted);

        var settings = services.GetService<ISiteService>() is { } siteService ? await siteService.GetSettingsAsync<SurveySettings>() : null;

        // The threshold counts completed, non-test responses only, so unfinished responses never pad the group.
        if (anonymous && SurveyAnonymity.IsTooSmall(survey, settings, (await queries.ListAsync(new SurveyResponseQuery
        {
            SurveyIds = [survey.ItemId],
            Statuses = [SurveyResponseStatus.Completed],
            Take = 1,
        }, httpContext.RequestAborted)).Count))
        {
            return Results.Ok(new { count = 0, hidden = true, items = Array.Empty<object>() });
        }

        var timeZone = anonymous && services.GetService<ILocalClock>() is { } localClock
            && TimeZoneInfo.TryFindSystemTimeZoneById((await localClock.GetLocalTimeZoneAsync())?.TimeZoneId ?? string.Empty, out var found)
            ? found
            : TimeZoneInfo.Utc;

        DateTime? Shown(DateTime? utc)
            => utc is not { } value ? null : anonymous ? SurveyAnonymity.StartOfDayUtc(value, timeZone) : DateTime.SpecifyKind(value, DateTimeKind.Utc);

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
                context = anonymous ? response.Context?.Kind : response.Context?.ToString(),
                startedUtc = Shown(response.StartedUtc),
                completedUtc = Shown(response.CompletedUtc),
                score = response.Score,
                maxScore = response.MaxScore,
                percent = response.Percent,
                passed = response.Passed,
            }),
        });
    }

    internal static async Task<IResult> StartAsync(StartResponseInput input, HttpContext httpContext, IAuthorizationService authorizationService, ISurveyResponseService responses, SurveyStore surveyStore, SurveyCollectorStore collectorStore, SurveyContextGuard contextGuard)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SurveysPermissions.RespondToSurveys))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(input?.SurveyId) && string.IsNullOrWhiteSpace(input?.CollectorToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["surveyId"] = ["Give the survey id or a collector token."] });
        }

        var identity = new SurveyRespondentIdentity { UserId = UserId(httpContext.User) };
        RecordRef? context = null;

        if (!string.IsNullOrWhiteSpace(input.Context))
        {
            if (!RecordRef.TryParse(input.Context, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["context"] = ["Write the context as Kind:Id."] });
            }

            // A context counts attempts and starts workflows, so a caller may only name a registered kind and a record
            // the kind's ownership check says the caller owns.
            var survey = !string.IsNullOrWhiteSpace(input.CollectorToken)
                ? await FindByCollectorAsync(input.CollectorToken, surveyStore, collectorStore, httpContext.RequestAborted)
                : await surveyStore.FindByIdAsync(input.SurveyId, httpContext.RequestAborted);

            if (survey is null)
            {
                return Problem(SurveyErrorCode.NotFound);
            }

            if (!await contextGuard.IsAllowedAsync(survey, parsed, identity, httpContext.RequestAborted))
            {
                return Problem(SurveyErrorCode.NotOwner);
            }

            context = parsed;
        }

        var result = await responses.StartAsync(new StartResponseRequest
        {
            SurveyId = input.SurveyId,
            CollectorToken = input.CollectorToken,
            Identity = identity,
            Context = context,

            // API variables are as untrusted as query string values: only hidden variables that accept a value from the
            // URL take them, and the collector's values are kept otherwise.
            QueryVariables = input.Variables ?? [],
            Culture = input.Culture,
        }, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return Problem(result.Error);
        }

        var page = await responses.GetPageAsync(result.Response, httpContext.RequestAborted);

        return Results.Ok(PageResult(page, result.Resumed, httpContext));
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

        return Results.Ok(PageResult(await responses.GetPageAsync(response, httpContext.RequestAborted), resumed: true, httpContext));
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

        return result.Succeeded ? Results.Ok(PageResult(result, resumed: false, httpContext)) : Problem(result.Error);
    }

    private static object PageResult(SurveyPageResult result, bool resumed, HttpContext httpContext)
    {
        var sanitizer = httpContext?.RequestServices?.GetService<SurveyContentSanitizer>();

        return new
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
                    description = Html(sanitizer, result.Page.Description),
                    position = result.Page.Position,
                    pageCount = result.Page.PageCount,
                    isLast = result.Page.IsLast,
                    canGoBack = result.Page.CanGoBack,
                    deadlineUtc = result.Page.DeadlineUtc,
                    questions = result.Page.Questions.Where(question => !result.Page.HiddenQuestionIds.Contains(question.Id)).Select(question => Sanitized(sanitizer, question)).ToList(),
                },
        };
    }

    private static IResult Problem(SurveyErrorCode error)
    {
        var status = error switch
        {
            SurveyErrorCode.NotFound => StatusCodes.Status404NotFound,
            SurveyErrorCode.NotOwner or SurveyErrorCode.NotInAudience or SurveyErrorCode.SignInRequired
                or SurveyErrorCode.PasswordRequired or SurveyErrorCode.PasswordInvalid or SurveyErrorCode.CaptchaRequired
                or SurveyErrorCode.InvitationInvalid or SurveyErrorCode.InvitationUsed => StatusCodes.Status403Forbidden,
            SurveyErrorCode.Busy or SurveyErrorCode.AlreadyInProgress or SurveyErrorCode.InvalidState => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        return Results.Problem(statusCode: status, title: error.ToString(), type: "https://crestapps.com/surveys/errors/" + error.ToString().ToLowerInvariant());
    }

    private static async Task<Survey> FindByCollectorAsync(string token, SurveyStore surveyStore, SurveyCollectorStore collectorStore, CancellationToken cancellationToken)
    {
        var collector = await collectorStore.FindByTokenAsync(token.Trim(), cancellationToken);

        return collector is null ? null : await surveyStore.FindByIdAsync(collector.SurveyId, cancellationToken);
    }

    private static bool IsPooled(SurveyPage page, SurveyQuestion question)
        => !string.IsNullOrEmpty(question.PoolId) && (page.Pools ?? []).Any(pool => string.Equals(pool?.Id, question.PoolId, StringComparison.Ordinal));

    private static string Html(SurveyContentSanitizer sanitizer, string html)
        => sanitizer is null || string.IsNullOrEmpty(html) ? html : sanitizer.Sanitize(html);

    private static SurveyQuestionView Sanitized(SurveyContentSanitizer sanitizer, SurveyQuestionView view)
    {
        if (view is not null)
        {
            view.Title = Html(sanitizer, view.Title);
            view.Description = Html(sanitizer, view.Description);
        }

        return view;
    }

    private static string UserId(ClaimsPrincipal user)
        => user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
}
