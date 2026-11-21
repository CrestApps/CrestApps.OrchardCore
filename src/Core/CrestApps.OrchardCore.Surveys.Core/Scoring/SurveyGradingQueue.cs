using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Lists the responses that need a grader: every survey for a user with the Grade survey responses permission,
/// otherwise the surveys a grading scope or an access provider lets the user grade.
/// </summary>
public sealed class SurveyGradingQueue : ISurveyGradingQueue
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ISurveyGradingScope> _scopes;
    private readonly IEnumerable<ISurveyAuthorizationService> _surveyAuthorization;
    private readonly ICatalog<Survey> _surveys;
    private readonly ISurveyResponseQueries _queries;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyGradingQueue"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopes">The grading scopes.</param>
    /// <param name="surveyAuthorization">The survey authorization service, when registered.</param>
    /// <param name="surveys">The survey store.</param>
    /// <param name="queries">The response queries.</param>
    public SurveyGradingQueue(
        IAuthorizationService authorizationService,
        IEnumerable<ISurveyGradingScope> scopes,
        IEnumerable<ISurveyAuthorizationService> surveyAuthorization,
        ICatalog<Survey> surveys,
        ISurveyResponseQueries queries)
    {
        _authorizationService = authorizationService;
        _scopes = scopes;
        _surveyAuthorization = surveyAuthorization;
        _surveys = surveys;
        _queries = queries;
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

        return await _queries.ListAsync(new SurveyResponseQuery
        {
            SurveyIds = surveyIds,
            Statuses = [SurveyResponseStatus.Completed],
            Grading = GradingStatus.Pending,
            Skip = Math.Max(0, skip),
            Take = Math.Clamp(take, 1, SurveysConstants.PageSize),
        }, cancellationToken);
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
