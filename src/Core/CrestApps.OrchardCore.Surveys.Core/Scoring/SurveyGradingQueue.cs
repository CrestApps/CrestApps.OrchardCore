using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Lists the responses that need a grader: every survey for a user with the Grade survey responses permission,
/// otherwise the surveys a grading scope or an access provider lets the user grade. It never lists the grader's own
/// responses (<see cref="SurveyOwnWork"/>), nor the responses of an anonymous survey whose completed responses are
/// below its minimum group size (<see cref="SurveyGroupGate"/>), and lists an anonymous survey's responses in random-id
/// order rather than by when they started.
/// </summary>
public sealed class SurveyGradingQueue : ISurveyGradingQueue
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ISurveyGradingScope> _scopes;
    private readonly IEnumerable<ISurveyAuthorizationService> _surveyAuthorization;
    private readonly ICatalog<Survey> _surveys;
    private readonly ISurveyResponseQueries _queries;
    private readonly SurveyContextKindOptions _contextKinds;
    private readonly SurveyResultsQueries _results;
    private readonly SurveyAnonymousGroups _groups;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyGradingQueue"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopes">The grading scopes.</param>
    /// <param name="surveyAuthorization">The survey authorization service, when registered.</param>
    /// <param name="surveys">The survey store.</param>
    /// <param name="queries">The response queries.</param>
    /// <param name="contextKinds">The registered context kinds; responses of a kind graded on its owner's page are left out.</param>
    /// <param name="results">The results queries, for the surveys with responses to grade.</param>
    /// <param name="groups">The small-group rule of anonymous surveys.</param>
    public SurveyGradingQueue(
        IAuthorizationService authorizationService,
        IEnumerable<ISurveyGradingScope> scopes,
        IEnumerable<ISurveyAuthorizationService> surveyAuthorization,
        ICatalog<Survey> surveys,
        ISurveyResponseQueries queries,
        IOptions<SurveyContextKindOptions> contextKinds,
        SurveyResultsQueries results,
        SurveyAnonymousGroups groups)
    {
        _results = results;
        _groups = groups;
        _authorizationService = authorizationService;
        _scopes = scopes;
        _surveyAuthorization = surveyAuthorization;
        _surveys = surveys;
        _queries = queries;
        _contextKinds = contextKinds.Value;
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(ClaimsPrincipal user, string surveyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            return (0, []);
        }

        List<string> surveyIds;

        if (await _authorizationService.AuthorizeAsync(user, SurveysPermissions.GradeSurveyResponses))
        {
            surveyIds = string.IsNullOrEmpty(surveyId) ? [] : [surveyId];
        }
        else
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal);

            foreach (var scope in _scopes)
            {
                allowed.UnionWith(await scope.GetGradableSurveyIdsAsync(user, cancellationToken) ?? []);
            }

            if (!string.IsNullOrEmpty(surveyId))
            {
                if (!allowed.Contains(surveyId) && !await CanGradeAsync(user, surveyId, cancellationToken))
                {
                    return (0, []);
                }

                allowed = [surveyId];
            }

            if (allowed.Count == 0)
            {
                return (0, []);
            }

            surveyIds = allowed.ToList();
        }

        var (hidden, listsAnonymous) = await AnonymousSurveysAsync(surveyIds, cancellationToken);

        if (!string.IsNullOrEmpty(surveyId) && hidden.Contains(surveyId))
        {
            return (0, []);
        }

        return await _queries.ListAsync(new SurveyResponseQuery
        {
            SurveyIds = surveyIds,
            ExcludedSurveyIds = hidden.ToList(),

            // Nobody grades their own work.
            ExcludedUserId = SurveyOwnWork.UserIdOf(user),

            // A queue that lists anonymous responses reads in random-id order: by start time, an anonymous response would
            // sit next to the same person's named response on another survey, and give away who handed in when.
            OrderById = listsAnonymous,
            Statuses = [SurveyResponseStatus.Completed],
            Grading = GradingStatus.Pending,
            // The whole queue leaves out the kinds graded on their owner's page; one survey's queue shows all of its responses.
            ExcludedContextKinds = string.IsNullOrEmpty(surveyId)
                ? _contextKinds.Kinds.Values
                    .Where(descriptor => descriptor.HasOwnGradingQueue)
                    .Select(descriptor => descriptor.Kind)
                    .ToList()
                : [],
            Skip = Math.Max(0, skip),
            Take = Math.Clamp(take, 1, SurveysConstants.PageSize),
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the anonymous surveys with responses to grade whose completed responses are below the minimum group size
    /// (a grader can't open their answers one at a time before there is a crowd to hide in), and whether the queue
    /// lists any other anonymous survey.
    /// </summary>
    private async Task<(HashSet<string> Hidden, bool ListsAnonymous)> AnonymousSurveysAsync(IReadOnlyCollection<string> surveyIds, CancellationToken cancellationToken)
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        var listsAnonymous = false;
        var pending = (await _results.ListSurveysPendingGradingAsync(cancellationToken))
            .Where(id => surveyIds.Count == 0 || surveyIds.Contains(id, StringComparer.Ordinal))
            .ToArray();

        if (pending.Length == 0)
        {
            return (hidden, false);
        }

        foreach (var survey in await _surveys.GetAsync(pending, cancellationToken))
        {
            if (!SurveyAnonymity.IsAnonymous(survey))
            {
                continue;
            }

            if ((await _groups.DecideAsync(survey, null, [SurveyResponseStatus.Completed], cancellationToken)).HidesAll)
            {
                hidden.Add(survey.ItemId);
            }
            else
            {
                listsAnonymous = true;
            }
        }

        return (hidden, listsAnonymous);
    }

    /// <inheritdoc />
    public async Task<bool> CanOpenAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // GradeOwnSurveyResponses is implied by GradeSurveyResponses, so this covers both.
        if (await _authorizationService.AuthorizeAsync(user, SurveysPermissions.GradeOwnSurveyResponses))
        {
            return true;
        }

        foreach (var scope in _scopes)
        {
            if ((await scope.GetGradableSurveyIdsAsync(user, cancellationToken))?.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<bool> CanGradeAsync(ClaimsPrincipal user, string surveyId, CancellationToken cancellationToken = default)
    {
        if (user is null || string.IsNullOrEmpty(surveyId))
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, SurveysPermissions.GradeSurveyResponses))
        {
            return true;
        }

        foreach (var scope in _scopes)
        {
            if ((await scope.GetGradableSurveyIdsAsync(user, cancellationToken) ?? []).Contains(surveyId, StringComparer.Ordinal))
            {
                return true;
            }
        }

        if (_surveyAuthorization.FirstOrDefault() is not { } authorization)
        {
            return false;
        }

        var survey = await _surveys.FindByIdAsync(surveyId, cancellationToken);

        return survey is not null && await authorization.AuthorizeAsync(user, survey, SurveyOperation.Grade, cancellationToken);
    }
}
