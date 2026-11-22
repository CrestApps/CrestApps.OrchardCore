using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Formulas;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Telemetry;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Runs responses on the server: start, pages, autosave, live evaluation, immediate feedback, completion, deadlines,
/// reopening, voiding and deleting. Every write to a response runs under a lock per response on a fresh read, with
/// optimistic concurrency; observers run after the commit.
/// </summary>
public sealed class SurveyResponseService : ISurveyResponseService
{
    /// <summary>
    /// The number series of responses.
    /// </summary>
    public const string NumberSeries = "SurveyResponses";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions _cloneOptions = new();

    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyResponseStore _responseStore;
    private readonly SurveyInvitationStore _invitationStore;
    private readonly SurveyRuntimeVersions _versions;
    private readonly ISurveyVersionCache _versionCache;
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyLogicEngine _logic;
    private readonly SurveyFormulaService _formulas;
    private readonly SurveyPlanBuilder _planBuilder;
    private readonly ISurveyPoolCounter _poolCounter;
    private readonly SurveyAccessEvaluator _access;
    private readonly SurveyParticipationService _participation;
    private readonly SurveyResponseCounter _counter;
    private readonly SurveyPageViewBuilder _pageViews;
    private readonly SurveyEndingService _endings;
    private readonly INumberSequence _numberSequence;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly ILeasedWorkQueue<SurveyDeadlineWork> _deadlines;
    private readonly IEnumerable<ISurveyResponseObserver> _observers;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly IStringLocalizer S;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseService"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="responseStore">The response store.</param>
    /// <param name="invitationStore">The invitation store.</param>
    /// <param name="versions">The runtime versions.</param>
    /// <param name="versionCache">The version cache.</param>
    /// <param name="registry">The control registry.</param>
    /// <param name="logic">The logic engine.</param>
    /// <param name="formulas">The formula service.</param>
    /// <param name="planBuilder">The plan builder.</param>
    /// <param name="poolCounter">The pool counter.</param>
    /// <param name="access">The access evaluator.</param>
    /// <param name="participation">The participation service.</param>
    /// <param name="counter">The response counter.</param>
    /// <param name="pageViews">The page view builder.</param>
    /// <param name="endings">The ending service.</param>
    /// <param name="numberSequence">The number sequence.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="deadlines">The deadline queue.</param>
    /// <param name="observers">The response observers.</param>
    /// <param name="serviceProvider">The services, for optional scoring and accommodations.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    /// <param name="logger">The logger.</param>
    public SurveyResponseService(
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        SurveyResponseStore responseStore,
        SurveyInvitationStore invitationStore,
        SurveyRuntimeVersions versions,
        ISurveyVersionCache versionCache,
        ISurveyControlRegistry registry,
        SurveyLogicEngine logic,
        SurveyFormulaService formulas,
        SurveyPlanBuilder planBuilder,
        ISurveyPoolCounter poolCounter,
        SurveyAccessEvaluator access,
        SurveyParticipationService participation,
        SurveyResponseCounter counter,
        SurveyPageViewBuilder pageViews,
        SurveyEndingService endings,
        INumberSequence numberSequence,
        IDistributedLock distributedLock,
        ISession session,
        ILeasedWorkQueue<SurveyDeadlineWork> deadlines,
        IEnumerable<ISurveyResponseObserver> observers,
        IServiceProvider serviceProvider,
        IClock clock,
        IStringLocalizer<SurveyResponseService> stringLocalizer,
        ILogger<SurveyResponseService> logger)
    {
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _responseStore = responseStore;
        _invitationStore = invitationStore;
        _versions = versions;
        _versionCache = versionCache;
        _registry = registry;
        _logic = logic;
        _formulas = formulas;
        _planBuilder = planBuilder;
        _poolCounter = poolCounter;
        _access = access;
        _participation = participation;
        _counter = counter;
        _pageViews = pageViews;
        _endings = endings;
        _numberSequence = numberSequence;
        _distributedLock = distributedLock;
        _session = session;
        _deadlines = deadlines;
        _observers = observers;
        _serviceProvider = serviceProvider;
        _clock = clock;
        S = stringLocalizer;
        _logger = logger;
    }

    /// <summary>
    /// Gets the lock key of writes to a response.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The key.</returns>
    public static string ResponseLockKey(string responseId)
        => "Surveys:ResponseWrite:" + responseId;

    /// <summary>
    /// Gets the lock key of starting a response for a respondent. A resume token is a secret, so only its SHA-256 hash
    /// goes into the key (lock keys can end up in a distributed cache).
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="respondentKey">The respondent key, or <see langword="null"/>.</param>
    /// <param name="resumeToken">The caller's resume token, or <see langword="null"/>.</param>
    /// <returns>The key.</returns>
    public static string StartLockKey(string surveyId, string respondentKey, string resumeToken)
    {
        var who = !string.IsNullOrEmpty(respondentKey)
            ? respondentKey
            : !string.IsNullOrEmpty(resumeToken)
                ? "t:" + SurveyResumeTokens.Hash(resumeToken)
                : global::OrchardCore.IdGenerator.GenerateId();

        return $"Surveys:Start:{surveyId}:{who}";
    }

    /// <inheritdoc />
    public async Task<SurveyStartResult> StartAsync(StartResponseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identity = request.Identity ?? new SurveyRespondentIdentity();
        SurveyCollector collector = null;
        Survey survey = null;

        if (!string.IsNullOrWhiteSpace(request.CollectorToken))
        {
            collector = await _collectorStore.FindByTokenAsync(request.CollectorToken.Trim(), cancellationToken);
            survey = collector is null ? null : await _surveyStore.FindByIdAsync(collector.SurveyId, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.SurveyId))
        {
            survey = await _surveyStore.FindByIdAsync(request.SurveyId, cancellationToken);
            collector = survey is null ? null : await _collectorStore.FindDefaultAsync(survey.ItemId, cancellationToken);
        }

        if (survey is null)
        {
            return new SurveyStartResult { Error = SurveyErrorCode.NotFound };
        }

        SurveyInvitation invitation = null;

        if (!string.IsNullOrWhiteSpace(request.InvitationToken))
        {
            invitation = await _invitationStore.FindByTokenHashAsync(SurveyRespondentKeys.Hash(request.InvitationToken.Trim()), cancellationToken);

            if (invitation is null)
            {
                return new SurveyStartResult { Error = SurveyErrorCode.InvitationInvalid };
            }
        }

        var isTest = request.IsTest && identity.IsStaff;
        var version = isTest && request.UseDraft
            ? SurveyRuntimeVersions.Draft(survey)
            : await _versionCache.GetPublishedAsync(survey, cancellationToken);

        if (version?.Definition is null)
        {
            return new SurveyStartResult { Error = SurveyErrorCode.NotOpen };
        }

        var anonymous = survey.Access?.Anonymity == AnonymityMode.Anonymous;
        var respondentKey = isTest ? null : await Secrets.RespondentKeyAsync(survey, identity, invitation, cancellationToken);
        var lockKey = StartLockKey(survey.ItemId, respondentKey, identity.ResumeToken);
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(lockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new SurveyStartResult { Error = SurveyErrorCode.Busy };
        }

        var notifications = new List<Func<Task>>();
        SurveyStartResult result;

        await using (locker)
        {
            var now = _clock.UtcNow;
            var existing = await FindOpenAsync(survey, request, identity, invitation, anonymous, isTest, cancellationToken);

            if (existing is not null)
            {
                if (existing.DeadlineUtc is { } deadline && now >= deadline.AddSeconds(Math.Max(version.Definition.Timing?.GraceSeconds ?? 5, 0)))
                {
                    await SubmitAtDeadlineAsync(existing.ItemId, cancellationToken);
                }
                else if (await _access.CheckResumeAsync(new SurveyAccessContext
                {
                    // Who the respondent is may have changed since the response started (a learner withdrawn from the
                    // course, say), so the rules about that decide again before the response is handed back.
                    Survey = survey,
                    Version = version,
                    Collector = collector,
                    Invitation = invitation,
                    Identity = identity,
                    RespondentKey = respondentKey,
                    Context = existing.Context,
                    Response = existing,
                    UtcNow = now,
                    IsTest = existing.IsTest,
                }) is { IsAllowed: false } refusal)
                {
                    return new SurveyStartResult { Error = refusal.Error };
                }
                else if (IsOwner(existing, identity))
                {
                    return new SurveyStartResult { Response = existing, Resumed = true };
                }
                else
                {
                    // Found by its invitation: the personal link is the invitee's key, so this browser resumes the
                    // response with a new resume token and the token held before stops working. A response that
                    // belongs to a user account resumes only for that user. Nothing else hands a response to another
                    // browser: there is no resume link, so only the holder of the personal link can take it over.
                    if (string.IsNullOrEmpty(existing.ResumeTokenHash))
                    {
                        return new SurveyStartResult { Error = SurveyErrorCode.SignInRequired };
                    }

                    var reissued = await IssueResumeTokenAsync(existing.ItemId, cancellationToken);

                    return reissued is null
                        ? new SurveyStartResult { Error = SurveyErrorCode.Busy }
                        : new SurveyStartResult { Response = existing, Resumed = true, ResumeToken = reissued };
                }
            }

            var overrides = _serviceProvider.GetService<ISurveyOverrideService>();
            var surveyOverride = overrides is null || isTest
                ? null
                : await overrides.FindEffectiveAsync(survey.ItemId, identity.UserId, request.Context, cancellationToken);

            var accessContext = new SurveyAccessContext
            {
                Survey = survey,
                Version = version,
                Collector = collector,
                Invitation = invitation,
                Identity = identity,
                RespondentKey = respondentKey,
                Context = request.Context,
                Override = surveyOverride,
                Password = request.Password,
                UtcNow = now,
                IsTest = isTest,
            };

            var decision = await _access.CheckStartAsync(accessContext);

            if (!decision.IsAllowed)
            {
                return new SurveyStartResult { Error = decision.Error };
            }

            var definition = version.Definition;
            if (definition.Pages?.Any(page => page.Pools?.Any(pool => pool.EvenlyPresent) == true) == true)
            {
                await _poolCounter.PrepareAsync(survey.ItemId, cancellationToken);
            }

            var plan = _planBuilder.Build(survey.ItemId, definition);
            var response = new SurveyResponse
            {
                SurveyId = survey.ItemId,
                SurveyVersionId = version.ItemId,
                VersionNumber = version.Number,
                Number = "R" + (await _numberSequence.NextAsync(NumberSeries, cancellationToken: cancellationToken)).ToString("D7", CultureInfo.InvariantCulture),
                Status = SurveyResponseStatus.InProgress,
                Grading = definition.Scoring?.Enabled == true ? GradingStatus.Pending : GradingStatus.NotScored,
                CollectorId = collector?.ItemId,
                Context = StoredContext(survey, request.Context),
                Culture = request.Culture ?? collector?.Culture,
                IsTest = isTest,
                StartedUtc = now,
                LastActivityUtc = now,
                ModifiedUtc = now,
                Plan = plan,
            };

            if (!anonymous)
            {
                response.UserId = identity.UserId;
                response.Respondent = identity.Party;
                response.InvitationId = invitation?.ItemId;

                if (survey.Access?.CollectIpAddress == true)
                {
                    response.IpAddress = identity.IpAddress;
                }
            }

            else if (invitation is not null && !isTest)
            {
                // An anonymous response never names its invitation; the seal lets its completion mark the
                // invitation completed, so the personal link can't be used again.
                response.InvitationSeal = SurveyRespondentKeys.InvitationSeal(await Secrets.RevealAsync(survey, cancellationToken), invitation.ItemId);
            }

            response.AttemptNumber = await AttemptNumberAsync(survey, response, respondentKey, request, isTest);

            // Properties come from the caller and can name the person, so an anonymous response keeps only the link
            // back (a course item, say): its path and text name a page, not a person.
            if (request.Properties is not null)
            {
                foreach (var property in request.Properties)
                {
                    if (!anonymous)
                    {
                        response.Properties[property.Key] = property.Value?.DeepClone();
                    }
                    else if (AnonymousReturnProperty(property.Key, property.Value) is { } kept)
                    {
                        response.Properties[property.Key] = kept;
                    }
                }
            }

            SetVariables(definition, collector, request, response);
            _formulas.ComputeVariables(definition, response, SurveyRuntimeVersions.CacheKey(version));

            var state = _logic.Evaluate(definition, response, SurveyRuntimeVersions.CacheKey(version));
            var first = SurveyPlanNavigator.FirstShowing(plan, state) ?? plan.PageOrder.FirstOrDefault();

            response.CurrentPageId = first;

            if (first is not null)
            {
                response.PagePath.Add(first);
                response.PageVisits.Add(new SurveyPageVisit { PageId = first, EnteredUtc = now });
            }

            var minutes = definition.Timing?.TimeLimitMinutes;

            if (minutes is > 0)
            {
                response.DeadlineUtc = now.AddMinutes(minutes.Value + (surveyOverride?.ExtraMinutes ?? 0));
            }

            var closes = surveyOverride?.ClosesUtc ?? survey.Access?.ClosesUtc;

            if (response.DeadlineUtc is not null && closes is not null && response.DeadlineUtc > closes)
            {
                response.DeadlineUtc = closes;
            }

            string resumeToken = null;

            if (string.IsNullOrEmpty(response.UserId))
            {
                response.ItemId = UniqueIdOf(response);
                resumeToken = SurveyResumeTokens.Issue(response.ItemId);
                response.ResumeTokenHash = SurveyResumeTokens.Hash(resumeToken);
            }

            await _responseStore.CreateAsync(response, cancellationToken);

            if (!isTest)
            {
                await _participation.RecordStartedAsync(survey, respondentKey, request.Context, now);
                await _poolCounter.RecordAsync(survey.ItemId, SurveyPlanBuilder.EvenlyPresented(definition, plan), cancellationToken);

                if (invitation is not null && invitation.Status is SurveyInvitationStatus.Pending or SurveyInvitationStatus.Sent or SurveyInvitationStatus.Opened)
                {
                    invitation.Status = SurveyInvitationStatus.Started;
                    await _invitationStore.UpdateAsync(invitation, cancellationToken);
                }
            }

            if (response.DeadlineUtc is { } due && definition.Timing?.AutoSubmitAtDeadline != false)
            {
                await _deadlines.EnqueueAsync(new SurveyDeadlineWork { ResponseId = response.ItemId, NextAttemptUtc = due.AddSeconds(Math.Max(definition.Timing?.GraceSeconds ?? 5, 0)) }, cancellationToken);
            }

            // Commit before the lock is let go, so a second start for the same respondent finds this response.
            await _session.SaveChangesAsync(cancellationToken);

            var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response };
            notifications.Add(() => NotifyAsync(observer => observer.StartedAsync(context)));
            result = new SurveyStartResult { Response = response, ResumeToken = resumeToken };
        }

        await RunAsync(notifications);

        return result;
    }

    /// <inheritdoc />
    public async Task<SurveyResponse> FindForRespondentAsync(string responseId, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(responseId) || identity is null)
        {
            return null;
        }

        var response = await _responseStore.FindByIdAsync(responseId, cancellationToken);

        return IsOwner(response, identity) ? response : null;
    }

    /// <summary>
    /// Gets whether a caller owns a response: the signed-in user who responds, or the holder of its resume token.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="identity">The caller.</param>
    /// <returns><see langword="true"/> when the caller owns it.</returns>
    public static bool IsOwner(SurveyResponse response, SurveyRespondentIdentity identity)
    {
        if (response is null || identity is null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(response.UserId) && string.Equals(response.UserId, identity.UserId, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrEmpty(response.ResumeTokenHash)
            && string.Equals(SurveyResumeTokens.ResponseIdOf(identity.ResumeToken), response.ItemId, StringComparison.Ordinal)
            && SurveyResumeTokens.Matches(identity.ResumeToken, response.ResumeTokenHash);
    }

    /// <inheritdoc />
    public async Task<SurveyPageResult> GetPageAsync(SurveyResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var survey = await _surveyStore.FindByIdAsync(response.SurveyId, cancellationToken);
        var version = await _versions.GetAsync(survey, response, cancellationToken);

        if (survey is null || version?.Definition is null)
        {
            return new SurveyPageResult { Error = SurveyErrorCode.NotFound, Response = response };
        }

        if (response.Status != SurveyResponseStatus.InProgress)
        {
            return new SurveyPageResult { Response = response, Ended = true };
        }

        return new SurveyPageResult { Response = response, Page = BuildPage(version, response) };
    }

    /// <inheritdoc />
    public async Task<SurveyPageResult> SubmitPageAsync(SubmitPageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var started = Stopwatch.GetTimestamp();

        try
        {
            return await SubmitPageCoreAsync(request, cancellationToken);
        }
        finally
        {
            SurveysTelemetry.PagePostDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds);
        }
    }

    private Task<SurveyPageResult> SubmitPageCoreAsync(SubmitPageRequest request, CancellationToken cancellationToken)
    {
        return WriteOwnAsync(request.ResponseId, request.Identity, () => new SurveyPageResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            var refusal = await RefuseClosedAsync(survey, version, response, notifications, cancellationToken);

            if (refusal is not null)
            {
                return new SurveyPageResult { Error = refusal.Value, Response = response, Ended = response.Status != SurveyResponseStatus.InProgress };
            }

            if (!string.IsNullOrEmpty(request.PageId) && !string.Equals(request.PageId, response.CurrentPageId, StringComparison.Ordinal))
            {
                // A stale tab posted an older page: show the current page again without changing anything.
                return new SurveyPageResult { Error = SurveyErrorCode.Busy, Response = response, Page = BuildPage(version, response) };
            }

            var definition = version.Definition;
            var cacheKey = SurveyRuntimeVersions.CacheKey(version);
            var now = _clock.UtcNow;
            var readErrors = await ReadScreenAsync(definition, version, response, request, cancellationToken);
            var state = Settle(definition, response, cacheKey);

            response.LastActivityUtc = now;
            response.ModifiedUtc = now;

            if (request.Direction == SurveyPageDirection.Back)
            {
                if (CanGoBack(definition, response))
                {
                    LeaveScreen(response, now);
                    response.PagePath.RemoveAt(response.PagePath.Count - 1);
                    response.CurrentPageId = response.PagePath[response.PagePath.Count - 1];
                    response.PageVisits.Add(new SurveyPageVisit { PageId = response.CurrentPageId, EnteredUtc = now });
                }

                await _responseStore.UpdateAsync(response, cancellationToken);

                return new SurveyPageResult { Response = response, Page = BuildPage(version, response, state) };
            }

            var errors = await ValidateScreenAsync(definition, response, state, readErrors);

            if (errors.Count > 0 && SurveyPageTimer.PageTimeIsUp(definition, response, now))
            {
                // The page's time ran out: what is valid is kept, the rest is dropped, and the respondent moves on.
                foreach (var questionId in errors.Keys)
                {
                    response.Answers.Remove(questionId);
                }

                errors.Clear();
            }

            if (errors.Count > 0)
            {
                foreach (var questionId in errors.Keys)
                {
                    response.Answers.Remove(questionId);
                }

                await _responseStore.UpdateAsync(response, cancellationToken);

                return new SurveyPageResult { Error = SurveyErrorCode.InvalidAnswer, Response = response, Page = BuildPage(version, response, state), Errors = errors };
            }

            var leftScreen = response.CurrentPageId;
            LeaveScreen(response, now);
            var eventContext = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response, PageId = SurveyPlanNavigator.PageOf(response.Plan, leftScreen) };
            notifications.Add(() => NotifyAsync(observer => observer.PageCompletedAsync(eventContext)));

            var (next, ending) = NextScreen(definition, response, state, cacheKey);

            if (next is not null)
            {
                response.CurrentPageId = next;
                response.PagePath.Add(next);
                response.PageVisits.Add(new SurveyPageVisit { PageId = next, EnteredUtc = now });
                await _responseStore.UpdateAsync(response, cancellationToken);

                return new SurveyPageResult { Response = response, Page = BuildPage(version, response) };
            }

            var completion = await CompleteCoreAsync(survey, version, response, ending, autoSubmitted: false, request.Identity, notifications, cancellationToken);

            if (completion != SurveyErrorCode.None)
            {
                await _responseStore.UpdateAsync(response, cancellationToken);

                return new SurveyPageResult { Error = completion, Response = response, Page = BuildPage(version, response, state) };
            }

            return new SurveyPageResult { Response = response, Ended = true };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SurveySaveResult> SaveAsync(SavePageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await WriteOwnAsync<SurveyResponseResult>(request.ResponseId, request.Identity, () => new SurveySaveResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            if (response.Status != SurveyResponseStatus.InProgress)
            {
                return new SurveySaveResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            if (!string.IsNullOrEmpty(request.PageId) && !string.Equals(request.PageId, response.CurrentPageId, StringComparison.Ordinal))
            {
                return new SurveySaveResult { Error = SurveyErrorCode.Busy, Response = response };
            }

            var now = _clock.UtcNow;

            await ReadScreenAsync(version.Definition, version, response, request, cancellationToken);
            Settle(version.Definition, response, SurveyRuntimeVersions.CacheKey(version));
            response.LastActivityUtc = now;
            response.ModifiedUtc = now;
            await _responseStore.UpdateAsync(response, cancellationToken);

            return new SurveySaveResult { Response = response, SavedUtc = now };
        }, cancellationToken);

        return result as SurveySaveResult ?? new SurveySaveResult { Error = result.Error, Response = result.Response };
    }

    /// <inheritdoc />
    public async Task<SurveyEvaluateResult> EvaluateAsync(EvaluatePageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stored = await FindForRespondentAsync(request.ResponseId, request.Identity, cancellationToken);

        if (stored is null)
        {
            return new SurveyEvaluateResult { Error = SurveyErrorCode.NotFound };
        }

        var survey = await _surveyStore.FindByIdAsync(stored.SurveyId, cancellationToken);
        var version = await _versions.GetAsync(survey, stored, cancellationToken);

        if (version?.Definition is null || stored.Status != SurveyResponseStatus.InProgress)
        {
            return new SurveyEvaluateResult { Error = SurveyErrorCode.InvalidState, Response = stored };
        }

        // The answers are applied to a copy: an evaluation never changes the stored response.
        var response = Clone(stored);
        var definition = version.Definition;
        var cacheKey = SurveyRuntimeVersions.CacheKey(version);

        await ReadScreenAsync(definition, version, response, request, cancellationToken);
        _formulas.ComputeVariables(definition, response, cacheKey);

        var state = _logic.Evaluate(definition, response, cacheKey);
        var page = _pageViews.Build(definition, response, state, response.CurrentPageId);
        var result = new SurveyEvaluateResult
        {
            Response = stored,
            HiddenQuestionIds = page.HiddenQuestionIds,
            HiddenChoiceIds = page.HiddenChoiceIds,
            Variables = new Dictionary<string, string>(response.Variables, StringComparer.Ordinal),
        };

        foreach (var question in page.Questions)
        {
            result.Texts["title:" + question.Id] = question.Title;
            result.Texts["description:" + question.Id] = question.Description;

            foreach (var choice in question.Choices)
            {
                result.Texts["choice:" + question.Id + ":" + choice.Id] = choice.Text;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SurveyCheckResult> CheckAnswerAsync(CheckAnswerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await WriteOwnAsync<SurveyResponseResult>(request.ResponseId, request.Identity, () => new SurveyCheckResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            var definition = version.Definition;

            if (response.Status != SurveyResponseStatus.InProgress || definition.Scoring?.FeedbackMode != FeedbackMode.Immediate)
            {
                return new SurveyCheckResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            var question = FindQuestion(definition, request.QuestionId);

            if (question is null || !SurveyPlanNavigator.QuestionsOf(response.Plan, response.CurrentPageId).Contains(question.Id))
            {
                return new SurveyCheckResult { Error = SurveyErrorCode.NotFound, Response = response };
            }

            if (response.Answers.TryGetValue(question.Id, out var locked) && locked.CheckedUtc is not null)
            {
                return new SurveyCheckResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            var cacheKey = SurveyRuntimeVersions.CacheKey(version);
            var before = _logic.Evaluate(definition, response, cacheKey);
            var readErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            await ReadQuestionAsync(definition, response, before, question, request, readErrors, cancellationToken);

            var state = Settle(definition, response, cacheKey);
            var errors = await ValidateQuestionsAsync(definition, response, state, [question.Id], readErrors);

            if (errors.TryGetValue(question.Id, out var questionErrors))
            {
                return new SurveyCheckResult { Error = SurveyErrorCode.InvalidAnswer, Response = response, Errors = questionErrors };
            }

            if (!response.Answers.TryGetValue(question.Id, out var answer))
            {
                return new SurveyCheckResult { Error = SurveyErrorCode.InvalidAnswer, Response = response, Errors = [S["Answer the question before you check it."].Value] };
            }

            var now = _clock.UtcNow;
            var scorer = _registry.GetScorer(question.ControlType);
            var during = _serviceProvider.GetService<ISurveyReviewPolicy>() is { } policy
                ? policy.GetAllowedFor(survey, definition, response, question.Id, now)
                : definition.Review?.During ?? ReviewShows.None;
            var check = new SurveyCheckResult { Response = response };

            if (scorer is not null)
            {
                var scoring = new SurveyScoringContext
                {
                    Definition = definition,
                    Settings = definition.Scoring ?? new SurveyScoringSettings(),
                    ShownChoiceIds = response.Plan.ChoiceOrder.TryGetValue(question.Id, out var shown) ? shown : [],
                };

                var score = scorer.Score(question, answer, scoring);
                answer.AutoScore = score.Points;
                answer.MaxPoints = score.MaxPoints;

                var feedback = scorer.GetFeedback(question, answer);
                check.Feedback = FilterFeedback(feedback, during);

                if ((during & ReviewShows.Score) != 0)
                {
                    check.Points = score.Points;
                }
            }

            answer.CheckedUtc = now;
            response.LastActivityUtc = now;
            response.ModifiedUtc = now;
            await _responseStore.UpdateAsync(response, cancellationToken);

            return check;
        }, cancellationToken);

        return result as SurveyCheckResult ?? new SurveyCheckResult { Error = result.Error, Response = result.Response };
    }

    /// <inheritdoc />
    public Task<SurveyResponseResult> SubmitAtDeadlineAsync(string responseId, CancellationToken cancellationToken = default)
        => WriteAsync<SurveyResponseResult>(responseId, () => new SurveyResponseResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            if (response.Status != SurveyResponseStatus.InProgress || response.DeadlineUtc is null || _clock.UtcNow < response.DeadlineUtc.Value)
            {
                return new SurveyResponseResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            await SubmitExpiredAsync(survey, version, response, notifications, cancellationToken);

            return new SurveyResponseResult { Response = response };
        }, cancellationToken);

    /// <inheritdoc />
    public Task<SurveyResponseResult> ReopenAsync(string responseId, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default)
        => WriteOwnAsync<SurveyResponseResult>(responseId, identity, () => new SurveyResponseResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            if (response.Status != SurveyResponseStatus.Completed
                || version.Definition.Navigation?.AllowEditAfterSubmit != true
                || !SurveyResponseTransitions.Table.CanMove(response.Status, SurveyResponseStatus.InProgress)
                || survey.Status != SurveyStatus.Open && !response.IsTest)
            {
                return new SurveyResponseResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            var now = _clock.UtcNow;

            response.Status = SurveyResponseStatus.InProgress;
            response.CompletedUtc = null;
            response.EndingId = null;
            response.CurrentPageId = response.PagePath.LastOrDefault() ?? response.Plan.PageOrder.FirstOrDefault();
            response.PageVisits.Add(new SurveyPageVisit { PageId = response.CurrentPageId, EnteredUtc = now });
            response.LastActivityUtc = now;
            response.ModifiedUtc = now;
            await _responseStore.UpdateAsync(response, cancellationToken);

            var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response };
            notifications.Add(() => NotifyAsync(observer => observer.ReopenedAsync(context)));

            return new SurveyResponseResult { Response = response };
        }, cancellationToken);

    /// <inheritdoc />
    public Task<SurveyResponseResult> VoidAsync(string responseId, string actorId, string reason, CancellationToken cancellationToken = default)
        => WriteAsync<SurveyResponseResult>(responseId, () => new SurveyResponseResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            if (!SurveyResponseTransitions.Table.CanMove(response.Status, SurveyResponseStatus.Voided))
            {
                return new SurveyResponseResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            var now = _clock.UtcNow;

            response.ScoreHistory.Add(new ScoreChange
            {
                ChangedUtc = now,
                Actor = actorId,
                Kind = ScoreChangeKind.Voided,
                From = response.Score,
                To = response.Score,
                Reason = reason,
            });

            response.Status = SurveyResponseStatus.Voided;
            response.ModifiedUtc = now;
            await _responseStore.UpdateAsync(response, cancellationToken);

            var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response, ActorId = actorId };
            notifications.Add(() => NotifyAsync(observer => observer.VoidedAsync(context)));

            return new SurveyResponseResult { Response = response };
        }, cancellationToken);

    /// <inheritdoc />
    public Task<SurveyResponseResult> DeleteAsync(string responseId, string actorId, CancellationToken cancellationToken = default)
        => WriteAsync<SurveyResponseResult>(responseId, () => new SurveyResponseResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            await _responseStore.DeleteAsync(response, cancellationToken);

            var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response, ActorId = actorId };
            notifications.Add(() => NotifyAsync(observer => observer.DeletedAsync(context)));

            return new SurveyResponseResult { Response = response };
        }, cancellationToken, allowMissingVersion: true);

    /// <summary>
    /// Marks a response in progress as abandoned, for the maintenance task.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="idleBeforeUtc">The response must have been idle since before this time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<SurveyResponseResult> AbandonAsync(string responseId, DateTime idleBeforeUtc, CancellationToken cancellationToken = default)
        => WriteAsync<SurveyResponseResult>(responseId, () => new SurveyResponseResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            if (response.Status != SurveyResponseStatus.InProgress || response.LastActivityUtc >= idleBeforeUtc)
            {
                return new SurveyResponseResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            response.Status = SurveyResponseStatus.Abandoned;
            response.ModifiedUtc = _clock.UtcNow;
            await _responseStore.UpdateAsync(response, cancellationToken);

            var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response };
            notifications.Add(() => NotifyAsync(observer => observer.AbandonedAsync(context)));

            return new SurveyResponseResult { Response = response };
        }, cancellationToken, allowMissingVersion: true);

    private SurveyPageView BuildPage(SurveyVersion version, SurveyResponse response, SurveyLogicState state = null)
    {
        state ??= _logic.Evaluate(version.Definition, response, SurveyRuntimeVersions.CacheKey(version));

        if (string.IsNullOrEmpty(response.CurrentPageId) || !response.Plan.PageOrder.Contains(response.CurrentPageId))
        {
            response.CurrentPageId = SurveyPlanNavigator.FirstShowing(response.Plan, state) ?? response.Plan.PageOrder.FirstOrDefault();
        }

        return _pageViews.Build(version.Definition, response, state, response.CurrentPageId);
    }

    // Issues a new resume token for a response under its write lock and stores its hash, so the token held before
    // stops working. Returns null when the response has no resume token or the lock is busy.
    private async Task<string> IssueResumeTokenAsync(string responseId, CancellationToken cancellationToken)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(ResponseLockKey(responseId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return null;
        }

        await using (locker)
        {
            var response = await FindFreshAsync(responseId, cancellationToken);

            if (response is null || string.IsNullOrEmpty(response.ResumeTokenHash))
            {
                return null;
            }

            var token = SurveyResumeTokens.Issue(response.ItemId);

            response.ResumeTokenHash = SurveyResumeTokens.Hash(token);
            await _responseStore.UpdateAsync(response, cancellationToken);

            try
            {
                await _session.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                return null;
            }

            return token;
        }
    }

    // The value an anonymous response keeps of a caller's property: the return link's text, and its local path
    // without a query string or fragment (which could carry an id of the person); nothing else.
    private static JsonValue AnonymousReturnProperty(string name, JsonNode value)
    {
        if (value is not JsonValue node || !node.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (string.Equals(name, SurveyResponseProperties.ReturnText, StringComparison.Ordinal))
        {
            return JsonValue.Create(text);
        }

        if (!string.Equals(name, SurveyResponseProperties.ReturnUrl, StringComparison.Ordinal))
        {
            return null;
        }

        var end = text.IndexOfAny(['?', '#']);
        var path = end < 0 ? text : text[..end];

        // Only a local path: one slash, then not a second slash or a backslash (a protocol-relative or odd address).
        return path.StartsWith('/') && (path.Length == 1 || path[1] is not ('/' or '\\')) ? JsonValue.Create(path) : null;
    }

    private SurveyAnonymitySecrets Secrets
        => _serviceProvider.GetRequiredService<SurveyAnonymitySecrets>();

    // An anonymous response keeps a context only when its kind can't point to one person; otherwise its kind only.
    private RecordRef? StoredContext(Survey survey, RecordRef? context)
        => SurveyAnonymity.StoredContext(survey, context, _serviceProvider.GetService<IOptions<SurveyContextKindOptions>>()?.Value);

    // Finds the invitation an anonymous response was started from by recomputing the seal of each open invitation of
    // its collector: nothing stored links the two, so only the survey's protected secret can.
    private async Task<SurveyInvitation> FindSealedInvitationAsync(Survey survey, SurveyResponse response, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(response.CollectorId))
        {
            return null;
        }

        var secret = await Secrets.RevealAsync(survey, cancellationToken);
        var open = await _invitationStore.ListByCollectorAsync(
            response.CollectorId,
            [SurveyInvitationStatus.Pending, SurveyInvitationStatus.Sent, SurveyInvitationStatus.Opened, SurveyInvitationStatus.Started, SurveyInvitationStatus.Completed],
            int.MaxValue,
            cancellationToken);

        return open.FirstOrDefault(candidate => string.Equals(SurveyRespondentKeys.InvitationSeal(secret, candidate.ItemId), response.InvitationSeal, StringComparison.Ordinal));
    }

    private async Task<SurveyResponse> FindOpenAsync(Survey survey, StartResponseRequest request, SurveyRespondentIdentity identity, SurveyInvitation invitation, bool anonymous, bool isTest, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(identity.ResumeToken))
        {
            var byToken = await FindForRespondentAsync(SurveyResumeTokens.ResponseIdOf(identity.ResumeToken), identity, cancellationToken);

            if (byToken is { Status: SurveyResponseStatus.InProgress }
                && string.Equals(byToken.SurveyId, survey.ItemId, StringComparison.Ordinal)
                && byToken.IsTest == isTest
                && Nullable.Equals(byToken.Context, StoredContext(survey, request.Context)))
            {
                return byToken;
            }
        }

        if (!anonymous && !isTest && !string.IsNullOrEmpty(identity.UserId)
            && await _counter.FindInProgressAsync(survey.ItemId, identity.UserId, request.Context?.ToString()) is { } byUser)
        {
            return byUser;
        }

        // A personal invitation resumes its own response wherever the link is opened: a link scanner that opened it
        // first must not lock the invitee out.
        if (invitation is not null && !isTest)
        {
            return await _counter.FindInProgressByInvitationAsync(survey.ItemId, invitation.ItemId);
        }

        return null;
    }

    private async Task<int> AttemptNumberAsync(Survey survey, SurveyResponse response, string respondentKey, StartResponseRequest request, bool isTest)
    {
        if (isTest)
        {
            return 1;
        }

        if (!string.IsNullOrEmpty(response.UserId))
        {
            return await _counter.CountForUserAsync(survey.ItemId, response.UserId, request.Context?.ToString()) + 1;
        }

        if (string.IsNullOrEmpty(respondentKey))
        {
            return 1;
        }

        var counts = await _participation.CountAsync(survey, respondentKey, request.Context, _clock.UtcNow);

        return counts.Started.Total + 1;
    }

    private static void SetVariables(SurveyDefinition definition, SurveyCollector collector, StartResponseRequest request, SurveyResponse response)
    {
        foreach (var variable in definition.Variables ?? [])
        {
            if (string.IsNullOrEmpty(variable?.Name) || variable.Kind != VariableKind.Hidden)
            {
                continue;
            }

            string value = variable.DefaultValue;

            if (collector?.Variables is not null && collector.Variables.TryGetValue(variable.Name, out var collectorValue))
            {
                value = collectorValue;
            }

            if (variable.AcceptFromUrl && request.QueryVariables is not null && request.QueryVariables.TryGetValue(variable.Name, out var queryValue))
            {
                value = queryValue?.Length > 500 ? queryValue.Substring(0, 500) : queryValue;
            }

            if (request.Variables is not null && request.Variables.TryGetValue(variable.Name, out var consumerValue))
            {
                value = consumerValue;
            }

            if (value is not null)
            {
                response.Variables[variable.Name] = value;
            }
        }
    }

    private async Task<Dictionary<string, List<string>>> ReadScreenAsync(SurveyDefinition definition, SurveyVersion version, SurveyResponse response, SubmitPageRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // Calculated questions and variables are current before the page is read, so their controls show their value.
        _formulas.ComputeVariables(definition, response, SurveyRuntimeVersions.CacheKey(version));

        var before = _logic.Evaluate(definition, response, SurveyRuntimeVersions.CacheKey(version));

        foreach (var questionId in SurveyPlanNavigator.QuestionsOf(response.Plan, response.CurrentPageId))
        {
            var question = FindQuestion(definition, questionId);

            if (question is not null)
            {
                await ReadQuestionAsync(definition, response, before, question, request, errors, cancellationToken);
            }
        }

        return errors;
    }

    private async Task ReadQuestionAsync(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, SurveyQuestion question, SubmitPageRequest request, Dictionary<string, List<string>> errors, CancellationToken cancellationToken)
    {
        var handler = _registry.GetHandler(question.ControlType);
        var descriptor = _registry.GetControl(question.ControlType);

        if (handler is null || descriptor?.Has(SurveyControlCapabilities.Answerable) == false || state.DisabledQuestionIds.Contains(question.Id))
        {
            return;
        }

        response.Answers.TryGetValue(question.Id, out var existing);

        // An answer checked with immediate feedback is locked.
        if (existing?.CheckedUtc is not null)
        {
            return;
        }

        var context = new SurveyAnswerReadContext
        {
            Question = question,
            Response = response,
            Definition = definition,
            Form = request.Form,
            Json = request.Answers?[question.Id],
            Prefix = SurveysConstants.AnswerPrefix + "." + question.Id,
            Existing = existing,
            AllowedChoiceIds = SurveyPageViewBuilder.AllowedChoices(definition, question, response, state),
            CancellationToken = cancellationToken,
        };

        if (request.Form is null && request.Answers is not null && !request.Answers.ContainsKey(question.Id))
        {
            // A JSON post that leaves a question out keeps its answer.
            return;
        }

        await handler.ReadAnswerAsync(context);

        if (context.Errors.Count > 0)
        {
            errors[question.Id] = [.. context.Errors];
        }

        if (context.Answer is null || handler.IsEmpty(question, context.Answer))
        {
            response.Answers.Remove(question.Id);

            return;
        }

        context.Answer.AnsweredUtc = _clock.UtcNow;
        response.Answers[question.Id] = context.Answer;
    }

    private SurveyLogicState Settle(SurveyDefinition definition, SurveyResponse response, string cacheKey)
    {
        _formulas.ComputeVariables(definition, response, cacheKey);

        var state = _logic.Evaluate(definition, response, cacheKey);

        // The answers of questions that are hidden now are dropped: one pass is enough because the evaluation already
        // resolved the chain of rules.
        foreach (var questionId in response.Answers.Keys.ToList())
        {
            if (state.HiddenQuestionIds.Contains(questionId))
            {
                response.Answers.Remove(questionId);
            }
            else if (state.HiddenChoiceIds.TryGetValue(questionId, out var hiddenChoices) && response.Answers[questionId].Values is { Count: > 0 } values)
            {
                values.RemoveAll(hiddenChoices.Contains);
            }
        }

        _formulas.ComputeVariables(definition, response, cacheKey);

        return state;
    }

    private Task<Dictionary<string, List<string>>> ValidateScreenAsync(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, Dictionary<string, List<string>> readErrors)
        => ValidateQuestionsAsync(definition, response, state, SurveyPlanNavigator.QuestionsOf(response.Plan, response.CurrentPageId), readErrors);

    private async Task<Dictionary<string, List<string>>> ValidateQuestionsAsync(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, IEnumerable<string> questionIds, Dictionary<string, List<string>> readErrors)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var questionId in questionIds)
        {
            var question = FindQuestion(definition, questionId);
            var handler = question is null ? null : _registry.GetHandler(question.ControlType);

            if (handler is null
                || state.HiddenQuestionIds.Contains(questionId)
                || state.DisabledQuestionIds.Contains(questionId)
                || _registry.GetControl(question.ControlType)?.Has(SurveyControlCapabilities.Answerable) == false)
            {
                continue;
            }

            response.Answers.TryGetValue(questionId, out var answer);
            var required = state.RequiredQuestionIds.Contains(questionId);
            var context = new SurveyAnswerValidationContext
            {
                Question = question,
                Answer = answer,
                IsRequired = required,
                Definition = definition,
                Response = response,
                AllowedChoiceIds = SurveyPageViewBuilder.AllowedChoices(definition, question, response, state),
            };

            if (readErrors.TryGetValue(questionId, out var read))
            {
                context.Errors.AddRange(read);
            }

            if (answer is not null || required)
            {
                await handler.ValidateAnswerAsync(context);
            }

            if (required && handler.IsEmpty(question, answer) && context.Errors.Count == 0)
            {
                context.Errors.Add(S["This question requires an answer."]);
            }

            if (context.Errors.Count > 0)
            {
                errors[questionId] = context.Errors.Distinct(StringComparer.Ordinal).ToList();
            }
        }

        return errors;
    }

    private static bool CanGoBack(SurveyDefinition definition, SurveyResponse response)
    {
        if (response.PagePath.Count < 2 || definition.Navigation?.AllowBack == false || definition.Navigation?.Sequential == true)
        {
            return false;
        }

        var pageId = SurveyPlanNavigator.PageOf(response.Plan, response.CurrentPageId);
        var page = definition.Pages?.FirstOrDefault(item => string.Equals(item.Id, pageId, StringComparison.Ordinal));

        return page?.AllowBack != false;
    }

    private (string Next, SurveyEnding Ending) NextScreen(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, string cacheKey)
    {
        var plan = response.Plan;
        var current = response.CurrentPageId;
        var index = plan.PageOrder.IndexOf(current);

        if (SurveyPlanNavigator.IsLastScreenOfPage(plan, current))
        {
            var pageId = SurveyPlanNavigator.PageOf(plan, current);
            var page = definition.Pages?.FirstOrDefault(item => string.Equals(item.Id, pageId, StringComparison.Ordinal));

            foreach (var branch in page?.Branches ?? [])
            {
                if (branch is null || !_logic.Holds(definition, response, state, branch.Rule, branch.Formula, cacheKey))
                {
                    continue;
                }

                if (branch.TargetKind == BranchTargetKind.Ending)
                {
                    var ending = definition.Endings?.FirstOrDefault(item => string.Equals(item.Id, branch.TargetId, StringComparison.Ordinal));

                    if (ending is not null)
                    {
                        return (null, ending);
                    }

                    continue;
                }

                var targetIndex = plan.PageOrder.FindIndex(index + 1, screen => string.Equals(SurveyPlanNavigator.PageOf(plan, screen), branch.TargetId, StringComparison.Ordinal));

                if (targetIndex < 0)
                {
                    // A branch only goes forward; publishing refuses one that does not.
                    continue;
                }

                var target = SurveyPlanNavigator.FirstShowing(plan, state, targetIndex);

                // The answers of the pages the branch skips are dropped.
                for (var skipped = index + 1; skipped < (target is null ? plan.PageOrder.Count : plan.PageOrder.IndexOf(target)); skipped++)
                {
                    foreach (var questionId in SurveyPlanNavigator.QuestionsOf(plan, plan.PageOrder[skipped]))
                    {
                        response.Answers.Remove(questionId);
                    }
                }

                return (target, target is null ? _endings.Choose(definition, response, state, cacheKey) : null);
            }
        }

        var next = SurveyPlanNavigator.FirstShowing(plan, state, index + 1);

        return next is null ? (null, _endings.Choose(definition, response, state, cacheKey)) : (next, null);
    }

    private async Task<SurveyErrorCode> CompleteCoreAsync(Survey survey, SurveyVersion version, SurveyResponse response, SurveyEnding ending, bool autoSubmitted, SurveyRespondentIdentity identity, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync("Surveys:Complete:" + survey.ItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return SurveyErrorCode.Busy;
        }

        await using (locker)
        {
            var now = _clock.UtcNow;
            var definition = version.Definition;
            SurveyInvitation invitation = null;

            if (!string.IsNullOrEmpty(response.InvitationId))
            {
                invitation = await _invitationStore.FindByIdAsync(response.InvitationId, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(response.InvitationSeal) && !response.IsTest)
            {
                invitation = await FindSealedInvitationAsync(survey, response, cancellationToken);
            }

            var respondentKey = response.IsTest || identity is null ? null : await Secrets.RespondentKeyAsync(survey, identity, invitation, cancellationToken);

            if (!autoSubmitted)
            {
                var overrides = _serviceProvider.GetService<ISurveyOverrideService>();
                var decision = await _access.CheckCompleteAsync(new SurveyAccessContext
                {
                    Survey = survey,
                    Version = version,
                    Collector = string.IsNullOrEmpty(response.CollectorId) ? null : await _collectorStore.FindByIdAsync(response.CollectorId, cancellationToken),
                    Invitation = invitation,
                    Identity = identity ?? new SurveyRespondentIdentity(),
                    RespondentKey = respondentKey,
                    Context = response.Context,
                    Response = response,
                    Override = overrides is null || response.IsTest ? null : await overrides.FindEffectiveAsync(survey.ItemId, response.UserId, response.Context, cancellationToken),
                    UtcNow = now,
                    IsTest = response.IsTest,
                });

                if (!decision.IsAllowed)
                {
                    return decision.Error;
                }
            }

            LeaveScreen(response, now);

            var scoring = definition.Scoring?.Enabled == true ? _serviceProvider.GetService<ISurveyScoringEngine>() : null;

            if (scoring is not null)
            {
                var scoringStarted = Stopwatch.GetTimestamp();

                scoring.Score(definition, response);
                SurveysTelemetry.ScoringDuration.Record(Stopwatch.GetElapsedTime(scoringStarted).TotalSeconds);
            }
            else if (definition.Scoring?.Enabled != true)
            {
                response.Grading = GradingStatus.NotScored;
            }

            var logicState = _logic.Evaluate(definition, response, SurveyRuntimeVersions.CacheKey(version));
            var quotas = response.IsTest || definition.Quotas is not { Count: > 0 } ? null : _serviceProvider.GetService<ISurveyQuotaService>();
            var screenedByQuota = false;

            ending ??= _endings.Choose(definition, response, logicState, SurveyRuntimeVersions.CacheKey(version));

            if (quotas is not null && ending?.Outcome != EndingOutcome.ScreenedOut
                && await quotas.FindFullAsync(survey, version, response, logicState, cancellationToken) is { } fullQuota)
            {
                // A respondent whose group is full is screened out with the quota's ending.
                screenedByQuota = true;
                ending = definition.Endings?.FirstOrDefault(candidate => candidate.Id == fullQuota.EndingId)
                    ?? definition.Endings?.FirstOrDefault(candidate => candidate.Outcome == EndingOutcome.ScreenedOut)
                    ?? ending;
            }

            response.EndingId = ending?.Id;
            response.Status = screenedByQuota || ending?.Outcome == EndingOutcome.ScreenedOut ? SurveyResponseStatus.ScreenedOut : SurveyResponseStatus.Completed;
            response.CompletedUtc = now;
            response.AutoSubmitted = autoSubmitted;
            response.LastActivityUtc = now;
            response.ModifiedUtc = now;

            if (!response.IsTest)
            {
                if (response.Status == SurveyResponseStatus.Completed)
                {
                    await _participation.RecordCompletedAsync(survey, respondentKey, response.Context, now);

                    if (quotas is not null)
                    {
                        var reached = await quotas.RecordAsync(survey, version, response, logicState, cancellationToken);

                        if (reached.Count > 0)
                        {
                            notifications.Add(() => quotas.NotifyReachedAsync(survey, reached, CancellationToken.None));
                        }
                    }
                }

                if (invitation is not null)
                {
                    invitation.Status = SurveyInvitationStatus.Completed;

                    if (survey.Access?.Anonymity != AnonymityMode.Anonymous)
                    {
                        invitation.ResponseId = response.ItemId;
                    }

                    await _invitationStore.UpdateAsync(invitation, cancellationToken);
                }

                // The seal has done its work: a completed anonymous response keeps nothing of its invitation.
                response.InvitationSeal = null;
            }

            var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response };

            await NotifyAsync(observer => observer.CompletingAsync(context));
            await _responseStore.UpdateAsync(response, cancellationToken);

            // Commit while the completion lock is held, so a parallel completion counts this one.
            await _session.SaveChangesAsync(cancellationToken);

            if (response.Status == SurveyResponseStatus.ScreenedOut)
            {
                notifications.Add(() => NotifyAsync(observer => observer.ScreenedOutAsync(context)));
            }
            else
            {
                notifications.Add(() => NotifyAsync(observer => observer.CompletedAsync(context)));

                if (response.Grading == GradingStatus.Pending && scoring is not null)
                {
                    notifications.Add(() => NotifyAsync(observer => observer.NeedsGradingAsync(context)));
                }
                else if (response.Grading == GradingStatus.Graded)
                {
                    notifications.Add(() => NotifyAsync(observer => observer.GradedAsync(context)));
                }
            }
        }

        return SurveyErrorCode.None;
    }

    private async Task SubmitExpiredAsync(Survey survey, SurveyVersion version, SurveyResponse response, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        var state = Settle(version.Definition, response, SurveyRuntimeVersions.CacheKey(version));
        var ending = _endings.Choose(version.Definition, response, state, SurveyRuntimeVersions.CacheKey(version));

        await CompleteCoreAsync(survey, version, response, ending, autoSubmitted: true, identity: null, notifications, cancellationToken);
    }

    private async Task<SurveyErrorCode?> RefuseClosedAsync(Survey survey, SurveyVersion version, SurveyResponse response, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        if (response.Status != SurveyResponseStatus.InProgress)
        {
            return SurveyErrorCode.InvalidState;
        }

        var grace = Math.Max(version.Definition.Timing?.GraceSeconds ?? 5, 0);

        if (response.DeadlineUtc is { } deadline && _clock.UtcNow >= deadline.AddSeconds(grace))
        {
            await SubmitExpiredAsync(survey, version, response, notifications, cancellationToken);

            return SurveyErrorCode.DeadlinePassed;
        }

        return null;
    }

    private static void LeaveScreen(SurveyResponse response, DateTime now)
    {
        var visit = response.PageVisits.LastOrDefault();

        if (visit is not null && visit.LeftUtc is null && string.Equals(visit.PageId, response.CurrentPageId, StringComparison.Ordinal))
        {
            visit.LeftUtc = now;
        }
    }

    private static SurveyAnswerFeedback FilterFeedback(SurveyAnswerFeedback feedback, ReviewShows shows)
    {
        if (feedback is null || shows == ReviewShows.None)
        {
            return null;
        }

        return new SurveyAnswerFeedback
        {
            Correct = (shows & (ReviewShows.Feedback | ReviewShows.RightAnswers | ReviewShows.Score)) != 0 ? feedback.Correct : null,
            RightAnswer = (shows & ReviewShows.RightAnswers) != 0 ? feedback.RightAnswer : null,
            CorrectChoiceIds = (shows & ReviewShows.RightAnswers) != 0 ? feedback.CorrectChoiceIds : [],
            Feedback = (shows & ReviewShows.Feedback) != 0 ? feedback.Feedback : null,
            ChoiceFeedback = (shows & ReviewShows.Feedback) != 0 ? feedback.ChoiceFeedback : [],
            GeneralFeedback = (shows & ReviewShows.GeneralFeedback) != 0 ? feedback.GeneralFeedback : null,
        };
    }

    private static SurveyQuestion FindQuestion(SurveyDefinition definition, string questionId)
        => string.IsNullOrEmpty(questionId)
            ? null
            : definition.Pages?.SelectMany(page => page.Questions ?? []).FirstOrDefault(question => string.Equals(question?.Id, questionId, StringComparison.Ordinal));

    private static SurveyResponse Clone(SurveyResponse response)
        => JsonSerializer.Deserialize<SurveyResponse>(JsonSerializer.Serialize(response, _cloneOptions), _cloneOptions);

    private static string UniqueIdOf(SurveyResponse response)
        => string.IsNullOrEmpty(response.ItemId) ? global::OrchardCore.IdGenerator.GenerateId() : response.ItemId;

    private Task<T> WriteOwnAsync<T>(string responseId, SurveyRespondentIdentity identity, Func<T> busy, Func<Survey, SurveyVersion, SurveyResponse, IList<Func<Task>>, Task<T>> work, CancellationToken cancellationToken)
        where T : SurveyResponseResult, new()
        => WriteAsync(responseId, busy, (survey, version, response, notifications) => IsOwner(response, identity)
            ? work(survey, version, response, notifications)
            : Task.FromResult(new T { Error = SurveyErrorCode.NotOwner }), cancellationToken);

    private async Task<T> WriteAsync<T>(string responseId, Func<T> busy, Func<Survey, SurveyVersion, SurveyResponse, IList<Func<Task>>, Task<T>> work, CancellationToken cancellationToken, bool allowMissingVersion = false)
        where T : SurveyResponseResult, new()
    {
        if (string.IsNullOrEmpty(responseId))
        {
            return new T { Error = SurveyErrorCode.NotFound };
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(ResponseLockKey(responseId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return busy();
        }

        var notifications = new List<Func<Task>>();
        T result;

        await using (locker)
        {
            var response = await FindFreshAsync(responseId, cancellationToken);

            if (response is null)
            {
                return new T { Error = SurveyErrorCode.NotFound };
            }

            var survey = await _surveyStore.FindByIdAsync(response.SurveyId, cancellationToken);
            var version = await _versions.GetAsync(survey, response, cancellationToken);

            if (survey is null || (version?.Definition is null && !allowMissingVersion))
            {
                return new T { Error = SurveyErrorCode.NotFound, Response = response };
            }

            result = await work(survey, version, response, notifications);

            try
            {
                await _session.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                notifications.Clear();
                result = busy();
            }
        }

        await RunAsync(notifications);

        return result;
    }

    private async Task<SurveyResponse> FindFreshAsync(string responseId, CancellationToken cancellationToken)
    {
        var tracked = await _responseStore.FindByIdAsync(responseId, cancellationToken);

        if (tracked is null)
        {
            return null;
        }

        _responseStore.YesSqlSession.Detach(tracked, SurveysConstants.ResponsesCollectionName);

        return await _responseStore.FindByIdAsync(responseId, cancellationToken);
    }

    private static async Task RunAsync(List<Func<Task>> notifications)
    {
        foreach (var notify in notifications)
        {
            await notify();
        }
    }

    private async Task NotifyAsync(Func<ISurveyResponseObserver, Task> notify)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "A survey response observer {Observer} failed.", observer.GetType().Name);
            }
        }
    }
}
