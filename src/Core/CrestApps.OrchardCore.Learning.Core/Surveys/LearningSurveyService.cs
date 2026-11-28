using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

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
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;

    // The surveys this scope created for course items. A copied item gets one while it is created, before it is copied
    // (Orchard creates the copy, then copies the content into it); the copy replaces it on this scope's session, so no
    // child scope waits on a row the request hasn't committed (SQL Server's default locking holds such a read until the
    // command times out).
    private readonly HashSet<string> _createdInScope = new(StringComparer.Ordinal);

    // The owners of the surveys of every course item, read once per scope: every scope check of the access providers
    // asks for them.
    private IReadOnlyCollection<RecordRef> _courseItemOwners;

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
    /// <param name="contentDefinitionManager">The content definition manager, for the course item types.</param>
    /// <param name="session">The YesSql session, for the content index.</param>
    public LearningSurveyService(
        ISurveyManager surveyManager,
        IServiceProvider serviceProvider,
        ISurveyResponseQueries responseQueries,
        ICourseAccessService courseAccess,
        ICourseStructureService courseStructure,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ISession session)
    {
        _surveyManager = surveyManager;
        _serviceProvider = serviceProvider;
        _responseQueries = responseQueries;
        _courseAccess = courseAccess;
        _courseStructure = courseStructure;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
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

        _createdInScope.Add(survey.ItemId);
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

        // A copy made while the item was created (with a blank survey) is replaced: nobody responded to it yet. One this
        // scope created has nothing hanging off it and may not be committed, so it is removed on this scope's session
        // through the manager (its handlers and observers, so the audit trail records the delete); a survey's full delete
        // would commit its steps in child scopes, which wait on the uncommitted row.
        foreach (var existing in await _surveyManager.ListByOwnerAsync(owner, cancellationToken))
        {
            if (string.Equals(existing.ItemId, sourceSurveyId, StringComparison.Ordinal))
            {
                continue;
            }

            if (_createdInScope.Remove(existing.ItemId))
            {
                await _surveyManager.DeleteNewSurveyAsync(existing, cancellationToken);
            }
            else
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
    public Task<SurveyStartResult> StartAsync(ClaimsPrincipal user, ContentItem item, string surveyId, string returnUrl, CancellationToken cancellationToken = default)
        => StartAsync(user, item, surveyId, returnUrl, password: null, statementAccepted: false, cancellationToken);

    /// <inheritdoc />
    public async Task<SurveyStartResult> StartAsync(ClaimsPrincipal user, ContentItem item, string surveyId, string returnUrl, string password, bool statementAccepted, CancellationToken cancellationToken = default)
    {
        var (request, error) = await BuildStartRequestAsync(user, item, surveyId, returnUrl, cancellationToken);

        if (request is null)
        {
            return new SurveyStartResult { Error = error };
        }

        var responses = _serviceProvider.GetRequiredService<ISurveyResponseService>();

        // "Change your answers": a course survey takes one completed response per learner, so a learner whose version
        // allows changing a submitted response reopens it instead of starting another.
        if (await ReopenableResponseAsync(request, cancellationToken) is { } completed)
        {
            var reopened = await responses.ReopenAsync(completed.ItemId, request.Identity, cancellationToken);

            if (reopened.Succeeded)
            {
                return new SurveyStartResult { Response = reopened.Response, Resumed = true };
            }
        }

        request.Password = password;
        request.StatementAccepted = statementAccepted;

        return await responses.StartAsync(request, cancellationToken);
    }

    // The learner's completed response of a course survey (not a quiz, whose attempts are separate) that its version lets
    // them change. An anonymous survey keeps no response that names the learner, so it has none.
    private async Task<SurveyResponse> ReopenableResponseAsync(StartResponseRequest request, CancellationToken cancellationToken)
    {
        if (request.Context is not { } context
            || !string.Equals(context.Kind, LearningSurveyContexts.EnrollmentKind, StringComparison.Ordinal))
        {
            return null;
        }

        var survey = await _surveyManager.FindByIdAsync(request.SurveyId, cancellationToken);

        if (survey is null || string.Equals(survey.Kind, SurveyKinds.Quiz, StringComparison.Ordinal))
        {
            return null;
        }

        var responses = await ListEnrollmentResponsesAsync(survey.ItemId, context.Id, cancellationToken);

        // A response in progress is resumed by the start itself.
        if (responses.Any(response => response.Status == SurveyResponseStatus.InProgress))
        {
            return null;
        }

        var completed = responses
            .Where(response => response.Status == SurveyResponseStatus.Completed && !string.IsNullOrEmpty(response.SurveyVersionId))
            .OrderByDescending(response => response.CompletedUtc ?? response.StartedUtc)
            .FirstOrDefault();

        if (completed is null)
        {
            return null;
        }

        var version = await _serviceProvider.GetRequiredService<ISurveyVersionCache>().GetAsync(completed.SurveyVersionId, cancellationToken);

        return version?.Definition?.Navigation?.AllowEditAfterSubmit == true ? completed : null;
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

        if (!LearningEnrollmentAccessRule.MayStartAttempt(survey, access.Enrollment))
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

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<RecordRef>> ListCourseItemOwnersAsync(CancellationToken cancellationToken = default)
        => _courseItemOwners ??= await ReadCourseItemOwnersAsync(cancellationToken);

    private async Task<IReadOnlyCollection<RecordRef>> ReadCourseItemOwnersAsync(CancellationToken cancellationToken)
    {
        var types = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition?.Name == nameof(CourseItemPart)))
            .Select(type => type.Name)
            .ToArray();

        if (types.Length == 0)
        {
            return [];
        }

        // Index rows only: the latest version of each item, as IsOwnedByCourseItemAsync reads it.
        var rows = await _session.QueryIndex<ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Latest).ListAsync(cancellationToken);

        return rows
            .Select(row => row.ContentItemId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .Select(LearningSurveyContexts.Owner)
            .ToList();
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

        // A course survey or poll takes one completed response per learner, as the course records one completion;
        // "Change your answers" reopens it when its version allows. A quiz's attempts are its own setting.
        if (!string.Equals(survey.Kind, SurveyKinds.Quiz, StringComparison.Ordinal) && survey.Access.Limits?.Total is null)
        {
            survey.Access.Limits ??= new RespondentLimits();
            survey.Access.Limits.Total = 1;
            survey.Access.Limits.Counts = LimitCount.Completed;
        }
    }

    private static T CopyOf<T>(T value)
        where T : class
        => value is null ? null : JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value));
}
