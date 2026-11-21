using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Creates the surveys course items own and starts learners' responses through their enrollment.
/// </summary>
public sealed class LearningSurveyService : ILearningSurveyService
{
    private readonly ISurveyManager _surveyManager;
    private readonly ISurveyResponseService _responseService;
    private readonly ISurveyResponseQueries _responseQueries;
    private readonly ICourseAccessService _courseAccess;
    private readonly ICourseStructureService _courseStructure;
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyService"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="responseService">The response service.</param>
    /// <param name="responseQueries">The response queries.</param>
    /// <param name="courseAccess">The course access service.</param>
    /// <param name="courseStructure">The course structure service.</param>
    /// <param name="contentManager">The content manager.</param>
    public LearningSurveyService(
        ISurveyManager surveyManager,
        ISurveyResponseService responseService,
        ISurveyResponseQueries responseQueries,
        ICourseAccessService courseAccess,
        ICourseStructureService courseStructure,
        IContentManager contentManager)
    {
        _surveyManager = surveyManager;
        _responseService = responseService;
        _responseQueries = responseQueries;
        _courseAccess = courseAccess;
        _courseStructure = courseStructure;
        _contentManager = contentManager;
    }

    /// <inheritdoc />
    public async Task<Survey> CreateOwnedSurveyAsync(ContentItem item, string kind, string template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var title = string.IsNullOrWhiteSpace(item.DisplayText) ? kind : item.DisplayText;

        return await _surveyManager.CreateSurveyAsync(new SurveyCreateRequest
        {
            Title = title,
            Kind = kind,
            Template = template,
            Owner = LearningSurveyContexts.Owner(item.ContentItemId),
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SurveyStartResult> StartAsync(ClaimsPrincipal user, ContentItem item, string surveyId, string returnUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return new SurveyStartResult { Error = SurveyErrorCode.SignInRequired };
        }

        var survey = string.IsNullOrEmpty(surveyId) ? null : await _surveyManager.FindByIdAsync(surveyId, cancellationToken);

        if (survey is null || !LearningSurveyContexts.IsContentOwner(survey.Owner) || survey.Owner.Value.Id != item.ContentItemId)
        {
            return new SurveyStartResult { Error = SurveyErrorCode.NotFound };
        }

        var access = await _courseAccess.GetItemAccessAsync(user, item);

        if (access.Reason != CourseItemAccessReason.Enrolled || access.Enrollment is null || !string.Equals(access.Enrollment.UserId, userId, StringComparison.Ordinal))
        {
            return new SurveyStartResult { Error = SurveyErrorCode.NotInAudience };
        }

        var enrollment = access.Enrollment;
        var anonymous = survey.Access?.Anonymity == AnonymityMode.Anonymous;
        var properties = new JsonObject
        {
            [LearningSurveyContexts.CourseIdProperty] = enrollment.CourseId,
            [LearningSurveyContexts.CourseItemIdProperty] = item.ContentItemId,
        };

        // An anonymous course survey keeps nothing that names the learner: the enrollment stays off the response.
        if (!anonymous)
        {
            properties[LearningSurveyContexts.EnrollmentIdProperty] = enrollment.ItemId;
        }

        if (!string.IsNullOrEmpty(returnUrl))
        {
            properties[SurveyResponseProperties.ReturnUrl] = returnUrl;
            properties[SurveyResponseProperties.ReturnText] = item.DisplayText;
        }

        return await _responseService.StartAsync(new StartResponseRequest
        {
            SurveyId = survey.ItemId,
            Identity = new SurveyRespondentIdentity
            {
                UserId = userId,
                Party = new PartyRef(PartyKinds.User, userId),
            },
            Context = anonymous ? LearningSurveyContexts.Course(enrollment.CourseId) : LearningSurveyContexts.Enrollment(enrollment.ItemId),
            Properties = properties,
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SurveyResponse>> ListEnrollmentResponsesAsync(string surveyId, string enrollmentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId) || string.IsNullOrEmpty(enrollmentId))
        {
            return [];
        }

        var (_, items) = await _responseQueries.ListAsync(new SurveyResponseQuery
        {
            SurveyIds = [surveyId],
            ContextRef = LearningSurveyContexts.Enrollment(enrollmentId).ToString(),
            Take = SurveysConstants.PageSize,
        }, cancellationToken);

        return items;
    }

    /// <inheritdoc />
    public async Task<ContentItem> FindOwnerItemAsync(Survey survey)
    {
        if (!LearningSurveyContexts.IsContentOwner(survey?.Owner))
        {
            return null;
        }

        var item = await _contentManager.GetAsync(survey.Owner.Value.Id, VersionOptions.Published);

        return item is not null && !string.IsNullOrEmpty(_courseStructure.GetCourseId(item)) ? item : null;
    }
}
