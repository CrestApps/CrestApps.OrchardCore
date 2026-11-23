using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Creates the surveys course items own and starts learners' responses through their enrollment.
/// </summary>
public sealed class LearningSurveyService : ILearningSurveyService
{
    private readonly ISurveyManager _surveyManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISurveyResponseQueries _responseQueries;
    private readonly ICourseAccessService _courseAccess;
    private readonly ICourseStructureService _courseStructure;
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyService"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="serviceProvider">The services, which give the response service when a response starts. The response
    /// service is resolved then, not here: it runs the access rules and observers, and the Learning ones use this service.</param>
    /// <param name="responseQueries">The response queries.</param>
    /// <param name="courseAccess">The course access service.</param>
    /// <param name="courseStructure">The course structure service.</param>
    /// <param name="contentManager">The content manager.</param>
    public LearningSurveyService(
        ISurveyManager surveyManager,
        IServiceProvider serviceProvider,
        ISurveyResponseQueries responseQueries,
        ICourseAccessService courseAccess,
        ICourseStructureService courseStructure,
        IContentManager contentManager)
    {
        _surveyManager = surveyManager;
        _serviceProvider = serviceProvider;
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

        var survey = await _surveyManager.CreateSurveyAsync(new SurveyCreateRequest
        {
            Title = title,
            Kind = kind,
            Template = template,
            Owner = LearningSurveyContexts.Owner(item.ContentItemId),
        }, cancellationToken);

        if (survey is null)
        {
            return null;
        }

        ApplyCourseDefaults(survey);

        // Templates are records an administrator may remove; a quiz is scored even when it started blank.
        if (string.Equals(kind, SurveyKinds.Quiz, StringComparison.Ordinal) && survey.Draft is { Scoring.Enabled: false })
        {
            survey.Draft.Scoring.Enabled = true;
            await _surveyManager.SaveDraftAsync(survey, survey.Draft, cancellationToken: cancellationToken);
        }
        else
        {
            await _surveyManager.UpdateAsync(survey, data: null, cancellationToken);
        }

        return survey;
    }

    /// <inheritdoc />
    public async Task<Survey> CopyOwnedSurveyAsync(ContentItem item, string sourceSurveyId, string kind, string template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var owner = LearningSurveyContexts.Owner(item.ContentItemId);

        // A copy made while the item was created (with a blank survey) is replaced: nobody responded to it yet.
        foreach (var existing in await _surveyManager.ListByOwnerAsync(owner, cancellationToken))
        {
            if (!string.Equals(existing.ItemId, sourceSurveyId, StringComparison.Ordinal))
            {
                await _surveyManager.DeleteSurveyAsync(existing.ItemId, cancellationToken);
            }
        }

        var source = string.IsNullOrEmpty(sourceSurveyId) ? null : await _surveyManager.FindByIdAsync(sourceSurveyId, cancellationToken);

        if (source?.Draft is null)
        {
            return await CreateOwnedSurveyAsync(item, kind, template, cancellationToken);
        }

        var copy = await _surveyManager.CreateSurveyAsync(new SurveyCreateRequest
        {
            Title = string.IsNullOrWhiteSpace(item.DisplayText) ? source.Title : item.DisplayText,
            Kind = source.Kind,
            Definition = source.Draft,
            Owner = owner,
        }, cancellationToken);

        if (copy is null)
        {
            return null;
        }

        copy.Description = source.Description;
        copy.Tags = [.. source.Tags];
        copy.Access = CopyOf(source.Access) ?? new SurveyAccessSettings();
        copy.Retention = CopyOf(source.Retention) ?? new SurveyRetentionSettings();
        ApplyCourseDefaults(copy);

        await _surveyManager.UpdateAsync(copy, data: null, cancellationToken);

        // The copy opens like the original: a published original gives a published copy, so the copied item works at once.
        if (!string.IsNullOrEmpty(source.PublishedVersionId))
        {
            await _serviceProvider.GetRequiredService<ISurveyPublisher>().PublishAsync(copy.ItemId, null, item.Owner, cancellationToken);
            copy = await _surveyManager.FindByIdAsync(copy.ItemId, cancellationToken) ?? copy;
        }

        return copy;
    }

    /// <inheritdoc />
    public async Task<SurveyStartResult> StartAsync(ClaimsPrincipal user, ContentItem item, string surveyId, string returnUrl, CancellationToken cancellationToken = default)
    {
        var (request, error) = await BuildStartRequestAsync(user, item, surveyId, returnUrl, cancellationToken);

        if (request is null)
        {
            return new SurveyStartResult { Error = error };
        }

        return await _serviceProvider.GetRequiredService<ISurveyResponseService>().StartAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SurveyStartCheckResult> CheckStartAsync(ClaimsPrincipal user, ContentItem item, string surveyId, CancellationToken cancellationToken = default)
    {
        var (request, error) = await BuildStartRequestAsync(user, item, surveyId, null, cancellationToken);

        if (request is null)
        {
            return new SurveyStartCheckResult { Error = error };
        }

        // The start the learner would make, checked by the rules that start runs: limits with their counting mode and
        // accommodations included.
        return await _serviceProvider.GetRequiredService<ISurveyStartCheck>().CheckAsync(request, cancellationToken);
    }

    // The start request of a learner's response, built once for starting and for checking a start, so the two never
    // differ in the context, the identity or the survey they run on.
    private async Task<(StartResponseRequest Request, SurveyErrorCode Error)> BuildStartRequestAsync(ClaimsPrincipal user, ContentItem item, string surveyId, string returnUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        var userId = LearningSurveyUsers.GetUserId(user);

        if (string.IsNullOrEmpty(userId))
        {
            return (null, SurveyErrorCode.SignInRequired);
        }

        var survey = string.IsNullOrEmpty(surveyId) ? null : await _surveyManager.FindByIdAsync(surveyId, cancellationToken);

        if (survey is null || !LearningSurveyContexts.IsContentOwner(survey.Owner) || survey.Owner.Value.Id != item.ContentItemId)
        {
            return (null, SurveyErrorCode.NotFound);
        }

        var access = await _courseAccess.GetItemAccessAsync(user, item);

        if (access.Reason != CourseItemAccessReason.Enrolled || access.Enrollment is null || !string.Equals(access.Enrollment.UserId, userId, StringComparison.Ordinal))
        {
            return (null, SurveyErrorCode.NotInAudience);
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

        return (new StartResponseRequest
        {
            SurveyId = survey.ItemId,
            Identity = new SurveyRespondentIdentity
            {
                UserId = userId,
                Party = new PartyRef(PartyKinds.User, userId),
            },
            Context = anonymous ? LearningSurveyContexts.Course(enrollment.CourseId) : LearningSurveyContexts.Enrollment(enrollment.ItemId),
            Properties = properties,
        }, SurveyErrorCode.None);
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

    /// <inheritdoc />
    public async Task<ContentItem> FindLatestOwnerItemAsync(Survey survey)
    {
        if (!LearningSurveyContexts.IsContentOwner(survey?.Owner))
        {
            return null;
        }

        var item = await _contentManager.GetAsync(survey.Owner.Value.Id, VersionOptions.Latest);

        return item is not null && !string.IsNullOrEmpty(_courseStructure.GetCourseId(item)) ? item : null;
    }

    /// <inheritdoc />
    public async Task<bool> IsOwnedByCourseItemAsync(Survey survey)
    {
        if (!LearningSurveyContexts.IsContentOwner(survey?.Owner))
        {
            return false;
        }

        // Ownership does not depend on the publish state: a draft, an unpublished item or one moved out of its course
        // still owns its survey. Only Learning makes surveys owned by a content item, so one whose item is gone is a
        // course item's too, and stays closed.
        var item = await _contentManager.GetAsync(survey.Owner.Value.Id, VersionOptions.Latest);

        return item is null || item.Has<CourseItemPart>();
    }

    private static void ApplyCourseDefaults(Survey survey)
    {
        // Learners answer through their enrollment, so the survey needs a signed-in respondent and no public link.
        survey.Access ??= new SurveyAccessSettings();

        if (survey.Access.Audience == SurveyAudienceMode.Anyone)
        {
            survey.Access.Audience = SurveyAudienceMode.SignedIn;
        }

        survey.NoDefaultWebLink = true;
    }

    private static T CopyOf<T>(T value)
        where T : class
        => value is null ? null : JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value));
}
