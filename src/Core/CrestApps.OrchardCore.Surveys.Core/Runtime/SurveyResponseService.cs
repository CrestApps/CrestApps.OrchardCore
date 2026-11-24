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
public sealed partial class SurveyResponseService : ISurveyResponseService
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
        await _formulas.PrepareAsync();
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
                if (existing.DeadlineUtc is { } deadline && now >= deadline.AddSeconds(SurveyAvailability.GraceSeconds(version.Definition)))
                {
                    // The respondent's own call: the completion counts toward their limits like any other.
                    await SubmitAtDeadlineCoreAsync(existing.ItemId, identity, cancellationToken);
                }
                else if (await ClosedForAsync(survey, existing, identity, now.AddSeconds(-SurveyAvailability.GraceSeconds(version.Definition)), cancellationToken))
                {
                    // The survey closed (and the grace a write in flight gets passed) while the response was in progress:
                    // it takes no new answers, so it is submitted as it was saved, and the start below is refused as closed.
                    await SubmitAtCloseCoreAsync(existing.ItemId, identity, cancellationToken);
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
                // The site's sequence numbers responses in the order they start, so next to identified responses' numbers
                // an anonymous response's number could name its respondent: it gets none (staff see a label instead).
                Number = anonymous ? null : "R" + (await _numberSequence.NextAsync(NumberSeries, cancellationToken: cancellationToken)).ToString("D7", CultureInfo.InvariantCulture),
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

            // The key the start counts under, so a completion without the respondent's request (the deadline task)
            // counts toward the same person's limits. It is cleared when the response completes.
            response.RespondentKey = respondentKey;

            // What the accommodation granted, for the same reason: a completion with no user to look up (an anonymous
            // response submitted by the deadline task or upkeep) still finishes under the close and attempts it started
            // with. It is cleared when the response ends.
            response.Accommodation = AccommodationOf(surveyOverride);

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

            // The deadline never runs past the close the respondent is held to: the accommodation's, else the survey's,
            // and the link's when that comes first. A write in flight at that close keeps the deadline's grace.
            var closes = SurveyAvailability.ClosesUtc(survey, surveyOverride, collector);

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
                await _deadlines.EnqueueAsync(new SurveyDeadlineWork { ResponseId = response.ItemId, NextAttemptUtc = due.AddSeconds(SurveyAvailability.GraceSeconds(definition)) }, cancellationToken);
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

        await _formulas.PrepareAsync();
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
            var refusal = await RefuseClosedAsync(survey, version, response, request.Identity, notifications, cancellationToken);

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
            var pageTimeIsUp = SurveyPageTimer.PageTimeIsUp(definition, response, now);

            // Only a page posted forward may keep files, and only once it passes validation: they stay pending until then.
            var readMode = request.Direction == SurveyPageDirection.Back ? SurveyAnswerReadMode.Draft : SurveyAnswerReadMode.Commit;
            var pendingFiles = new Dictionary<string, List<SurveyPendingFile>>(StringComparer.Ordinal);
            var referencedFiles = SurveyResponseFiles.ReferencedIds(response);

            // The stored answers that keep files (uploads, a signature), as they were before this post: a refused page
            // puts them back, so a bad new file never takes the files kept earlier with it.
            var storedFileAnswers = StoredFileAnswers(response);

            // The response before this post, when the post could take a locked answer off the path or hide it.
            var lockSnapshot = LockSnapshot(definition, response);
            var screenQuestionIds = SurveyPlanNavigator.QuestionsOf(response.Plan, response.CurrentPageId);

            // A post whose answers would take a locked answer off the path or hide it is refused: nothing is saved, the
            // answers that would do it get their earlier value back, the respondent's other new answers stay on the
            // page, and the page shows why on the answers that would do it.
            SurveyPageResult RefuseLockBreak(Dictionary<string, List<string>> breaks)
            {
                RestoreLockBreakers(definition, cacheKey, response, lockSnapshot, breaks.Keys, screenQuestionIds);

                return new SurveyPageResult { Error = SurveyErrorCode.InvalidAnswer, Response = response, Page = BuildPage(version, response), Errors = breaks };
            }

            // Once the page's own time is up, what is posted for it is ignored: only what was saved in time counts.
            var readErrors = pageTimeIsUp
                ? new Dictionary<string, List<string>>(StringComparer.Ordinal)
                : await ReadScreenAsync(definition, version, response, request, readMode, pendingFiles, cancellationToken);
            var state = Settle(definition, response, cacheKey);

            response.LastActivityUtc = now;
            response.ModifiedUtc = now;

            if (request.Direction == SurveyPageDirection.Back)
            {
                // Going back keeps the page's answers as they are, so it is refused too when they would break a lock.
                if (FindLockBreaks(definition, cacheKey, lockSnapshot, response, screenQuestionIds) is { } backBreaks)
                {
                    return RefuseLockBreak(backBreaks);
                }

                if (CanGoBack(definition, response, now))
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

            if (errors.Count > 0 && pageTimeIsUp)
            {
                // The page's time ran out: what is valid is kept, the rest is dropped, and the respondent moves on.
                var beforeDrop = lockSnapshot is null ? null : Clone(response);

                RemoveUnlocked(response, errors.Keys);

                // Nothing posted is read once the time is up, so the respondent can no longer change these answers: an
                // answer saved in time whose drop would take a locked answer off the path or hide it stays as saved,
                // rather than refusing the page for good.
                if (FindLockBreaks(definition, cacheKey, lockSnapshot, response, errors.Keys) is { } keptForLocks)
                {
                    foreach (var questionId in keptForLocks.Keys)
                    {
                        if (beforeDrop.Answers.TryGetValue(questionId, out var saved))
                        {
                            response.Answers[questionId] = saved;
                        }
                    }
                }

                errors.Clear();
            }

            if (errors.Count > 0)
            {
                RemoveUnlocked(response, errors.Keys);

                // The page is refused: none of its new files are stored, and each question of the screen that kept files
                // before this post gets its stored answer back, so those files stay referenced and the orphan sweep
                // leaves them alone.
                SurveyResponseFiles.DiscardPending(response);
                RestoreFileAnswers(response, SurveyPlanNavigator.QuestionsOf(response.Plan, response.CurrentPageId).Where(id => !state.HiddenQuestionIds.Contains(id)), storedFileAnswers);

                // The valid answers of a refused page are kept, so they must keep the locked answers too.
                if (FindLockBreaks(definition, cacheKey, lockSnapshot, response, screenQuestionIds) is { } refusedBreaks)
                {
                    return RefuseLockBreak(refusedBreaks);
                }

                await _responseStore.UpdateAsync(response, cancellationToken);

                return new SurveyPageResult { Error = SurveyErrorCode.InvalidAnswer, Response = response, Page = BuildPage(version, response, state), Errors = errors };
            }

            // The answers move the respondent on (or finish): the path from here, with the answers a branch skips
            // dropped, must keep every locked answer. Once the page's time is up nothing posted was read, and the
            // answers dropped above already kept them.
            if (!pageTimeIsUp && FindLockBreaks(definition, cacheKey, lockSnapshot, response, screenQuestionIds) is { } forwardBreaks)
            {
                return RefuseLockBreak(forwardBreaks);
            }

            var fileErrors = await Files.StorePendingAsync(response, pendingFiles, cancellationToken);

            if (fileErrors.Count > 0)
            {
                // Each question whose new files were refused gets its stored answer back, so a replaced signature or file
                // stays referenced and is not removed below.
                RestoreFileAnswers(response, fileErrors.Keys, storedFileAnswers);
            }

            // Once committed, the files the response referred to under this lock and no longer refers to are removed.
            referencedFiles.UnionWith(SurveyResponseFiles.ReferencedIds(response));
            notifications.Add(() => Files.DeleteReleasedAsync(referencedFiles, response, CancellationToken.None));

            if (fileErrors.Count > 0)
            {
                await _responseStore.UpdateAsync(response, cancellationToken);

                return new SurveyPageResult { Error = SurveyErrorCode.InvalidAnswer, Response = response, Page = BuildPage(version, response, state), Errors = fileErrors };
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
            // The same deadline as a page post: a save after the time and its grace submits what was saved before.
            var refusal = await RefuseClosedAsync(survey, version, response, request.Identity, notifications, cancellationToken);

            if (refusal is not null)
            {
                return new SurveySaveResult { Error = refusal.Value, Response = response };
            }

            if (!string.IsNullOrEmpty(request.PageId) && !string.Equals(request.PageId, response.CurrentPageId, StringComparison.Ordinal))
            {
                return new SurveySaveResult { Error = SurveyErrorCode.Busy, Response = response };
            }

            var now = _clock.UtcNow;

            if (SurveyPageTimer.PageTimeIsUp(version.Definition, response, now))
            {
                // The page's own time is up: nothing posted for it is kept any more.
                return new SurveySaveResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            var lockSnapshot = LockSnapshot(version.Definition, response);

            await ReadScreenAsync(version.Definition, version, response, request, SurveyAnswerReadMode.Draft, pendingFiles: null, cancellationToken);
            Settle(version.Definition, response, SurveyRuntimeVersions.CacheKey(version));

            // An autosave whose answers would take a locked answer off the path or hide it saves nothing.
            if (FindLockBreaks(version.Definition, SurveyRuntimeVersions.CacheKey(version), lockSnapshot, response, SurveyPlanNavigator.QuestionsOf(response.Plan, response.CurrentPageId)) is { } breaks)
            {
                RestoreSnapshot(response, lockSnapshot);

                return new SurveySaveResult { Error = SurveyErrorCode.InvalidAnswer, Response = response, Errors = breaks };
            }

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

        await _formulas.PrepareAsync();
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

        await ReadScreenAsync(definition, version, response, request, SurveyAnswerReadMode.Draft, pendingFiles: null, cancellationToken);
        _formulas.ComputeVariables(definition, response, cacheKey);

        var state = _logic.Evaluate(definition, response, cacheKey);
        var page = _pageViews.Build(definition, response, state, response.CurrentPageId);
        var result = new SurveyEvaluateResult
        {
            Response = stored,
            HiddenQuestionIds = page.HiddenQuestionIds,
            HiddenChoiceIds = page.HiddenChoiceIds,
            Variables = new Dictionary<string, string>(response.Variables, StringComparer.Ordinal),

            // The answers a save or a page post would refuse because they'd take a locked answer off the path or hide
            // it, so the page can say so as the respondent changes them.
            Errors = (GuardsLocks(definition, stored) ? FindLockBreaks(definition, cacheKey, stored, response, SurveyPlanNavigator.QuestionsOf(response.Plan, response.CurrentPageId)) : null)
                ?? new Dictionary<string, List<string>>(StringComparer.Ordinal),
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

            // The same deadline as a page post: a check after the time and its grace submits what was saved before.
            var refusal = await RefuseClosedAsync(survey, version, response, request.Identity, notifications, cancellationToken);

            if (refusal is not null)
            {
                return new SurveyCheckResult { Error = refusal.Value, Response = response };
            }

            if (definition.Scoring?.FeedbackMode != FeedbackMode.Immediate || SurveyPageTimer.PageTimeIsUp(definition, response, _clock.UtcNow))
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
            var lockSnapshot = LockSnapshot(definition, response);
            var before = _logic.Evaluate(definition, response, cacheKey);
            var readErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            await ReadQuestionAsync(definition, response, before, question, request, readErrors, SurveyAnswerReadMode.Draft, pendingFiles: null, cancellationToken);

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

            // The answer is locked first: the review policy shows feedback during a response only for a checked answer.
            answer.CheckedUtc = now;

            // The answer checked may itself be read by a rule or a branch: it can't take an answer locked earlier off the
            // path or hide it, and then nothing is locked or saved.
            if (FindLockBreaks(definition, cacheKey, lockSnapshot, response, [question.Id]) is { } breaks)
            {
                RestoreSnapshot(response, lockSnapshot);

                return new SurveyCheckResult { Error = SurveyErrorCode.InvalidAnswer, Response = response, Errors = [.. breaks.Values.SelectMany(messages => messages).Distinct(StringComparer.Ordinal)] };
            }

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

            response.LastActivityUtc = now;
            response.ModifiedUtc = now;
            await _responseStore.UpdateAsync(response, cancellationToken);

            return check;
        }, cancellationToken);

        return result as SurveyCheckResult ?? new SurveyCheckResult { Error = result.Error, Response = result.Response };
    }

    /// <inheritdoc />
    public Task<SurveyResponseResult> SubmitAtDeadlineAsync(string responseId, CancellationToken cancellationToken = default)
        => SubmitAtDeadlineCoreAsync(responseId, identity: null, cancellationToken);

    // Submits a response whose deadline and grace passed. With no identity (the deadline task) the completion counts
    // under the key the response's start was counted under.
    private Task<SurveyResponseResult> SubmitAtDeadlineCoreAsync(string responseId, SurveyRespondentIdentity identity, CancellationToken cancellationToken)
        => WriteAsync<SurveyResponseResult>(responseId, () => new SurveyResponseResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            // The same grace as the respondent's own posts: a last page still in flight inside it is not cut off.
            var grace = SurveyAvailability.GraceSeconds(version.Definition);

            if (response.Status != SurveyResponseStatus.InProgress || response.DeadlineUtc is null || _clock.UtcNow < response.DeadlineUtc.Value.AddSeconds(grace))
            {
                return new SurveyResponseResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            var error = await SubmitExpiredAsync(survey, version, response, identity, notifications, cancellationToken);

            return new SurveyResponseResult { Error = error, Response = response };
        }, cancellationToken);

    /// <summary>
    /// Submits a response left in progress on a survey that closed, as it was saved before the close, for the upkeep
    /// task. The completion rules still decide: a response they refuse is abandoned rather than completed.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result; <see cref="SurveyErrorCode.InvalidState"/> when the response ended or the survey is still
    /// open to its respondent (an accommodation that closes later).</returns>
    public Task<SurveyResponseResult> SubmitAtCloseAsync(string responseId, CancellationToken cancellationToken = default)
        => SubmitAtCloseCoreAsync(responseId, identity: null, cancellationToken);

    private Task<SurveyResponseResult> SubmitAtCloseCoreAsync(string responseId, SurveyRespondentIdentity identity, CancellationToken cancellationToken)
        => WriteAsync<SurveyResponseResult>(responseId, () => new SurveyResponseResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            // A last page in flight at the closing date keeps its grace here too, so the upkeep doesn't cut it off.
            if (response.Status != SurveyResponseStatus.InProgress
                || !await ClosedForAsync(survey, response, identity, _clock.UtcNow.AddSeconds(-SurveyAvailability.GraceSeconds(version.Definition)), cancellationToken))
            {
                return new SurveyResponseResult { Error = SurveyErrorCode.InvalidState, Response = response };
            }

            var error = await SubmitExpiredAsync(survey, version, response, identity, notifications, cancellationToken);

            return new SurveyResponseResult { Error = error, Response = response };
        }, cancellationToken);

    // Whether the survey is closed to the respondent of a response: staff closed the survey or its link, or the closing
    // date passed (the respondent's accommodation's, else the survey's, and the link's). A test response never closes.
    private async Task<bool> ClosedForAsync(Survey survey, SurveyResponse response, SurveyRespondentIdentity identity, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (response.IsTest)
        {
            return false;
        }

        var collector = string.IsNullOrEmpty(response.CollectorId) ? null : await _collectorStore.FindByIdAsync(response.CollectorId, cancellationToken);

        if (SurveyAvailability.IsClosedByStatus(survey, collector))
        {
            return true;
        }

        // An accommodation moves the survey's closing date, so it is read only when the survey has one.
        var surveyOverride = survey.Access?.ClosesUtc is null ? null : await OverrideForAsync(survey, response, identity, cancellationToken);

        return SurveyAvailability.IsClosed(survey, surveyOverride, collector, utcNow);
    }

    // The accommodation of a response's respondent: the one of its user (the response's own, else the caller's), looked
    // up now, so one granted while the response is in progress applies and one revoked or cut down no longer does (with a
    // known user, finding none means none). What the start granted, kept on the response, applies only when no user can
    // be looked up (an anonymous response submitted by the deadline task or upkeep), or when the lookup can't match it
    // because the response keeps its context hidden (an anonymous response keeps the kind of an identifying context
    // only, so an accommodation for that context can't be found again).
    private async Task<SurveyOverride> OverrideForAsync(Survey survey, SurveyResponse response, SurveyRespondentIdentity identity, CancellationToken cancellationToken)
    {
        if (response.IsTest)
        {
            return null;
        }

        var userId = response.UserId ?? identity?.UserId;

        if (!string.IsNullOrEmpty(userId) && _serviceProvider.GetService<ISurveyOverrideService>() is { } overrides)
        {
            var found = await overrides.FindEffectiveAsync(survey.ItemId, userId, response.Context, cancellationToken);

            if (found is not null || !HasHiddenContext(survey, response))
            {
                return found;
            }
        }

        return response.Accommodation is { } granted
            ? new SurveyOverride
            {
                SurveyId = survey.ItemId,
                ClosesUtc = granted.ClosesUtc,
                ExtraAttempts = granted.ExtraAttempts,
            }
            : null;
    }

    // What an accommodation grants a response at its start, or null without one (or when it grants nothing that outlasts
    // the start: its extra time is already in the deadline).
    private static SurveyResponseAccommodation AccommodationOf(SurveyOverride surveyOverride)
        => surveyOverride is null || (surveyOverride.ClosesUtc is null && surveyOverride.ExtraAttempts <= 0)
            ? null
            : new SurveyResponseAccommodation { ClosesUtc = surveyOverride.ClosesUtc, ExtraAttempts = Math.Max(surveyOverride.ExtraAttempts, 0) };

    // Whether a response keeps only the kind of its context (an anonymous survey's identifying context), so a lookup of
    // its respondent's accommodations can't match one granted for that context.
    private static bool HasHiddenContext(Survey survey, SurveyResponse response)
        => response.Context is { } context
            && string.Equals(context.Id, SurveyAnonymity.HiddenContextId, StringComparison.Ordinal)
            && SurveyAnonymity.IsAnonymous(survey);

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

            // Once the survey closes (its closing date included, the moment the review after close may show the right
            // answers), no response is reopened to change. The survey's own closing date counts even for a respondent
            // whose accommodation closes later: the review after close follows the survey's date.
            if (!response.IsTest && SurveyAvailability.IsClosed(survey, surveyOverride: null, collector: null, _clock.UtcNow)
                || await ClosedForAsync(survey, response, identity, _clock.UtcNow, cancellationToken))
            {
                return new SurveyResponseResult { Error = SurveyErrorCode.Closed, Response = response };
            }

            // The counts the completion added are taken back under the completion lock, so completing again counts once.
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync("Surveys:Complete:" + survey.ItemId, _lockTimeout, _lockExpiration);

            if (!locked)
            {
                return new SurveyResponseResult { Error = SurveyErrorCode.Busy, Response = response };
            }

            await using (locker)
            {
                var now = _clock.UtcNow;

                await ReleaseCompletionAsync(survey, version, response, identity, cancellationToken);

                response.Status = SurveyResponseStatus.InProgress;
                response.CompletedUtc = null;
                response.EndingId = null;

                // The earlier completion's result goes too: rules on the score read the score of the next completion,
                // never an old one, and completing again scores the response afresh.
                response.Score = null;
                response.MaxScore = null;
                response.Percent = null;
                response.Passed = null;
                response.ScoreBandId = null;
                response.CategoryScores.Clear();
                response.UnreachedPagePath = [];

                if (version.Definition.Scoring?.Enabled == true)
                {
                    response.Grading = GradingStatus.Pending;
                }
                response.CurrentPageId = response.PagePath.LastOrDefault() ?? response.Plan.PageOrder.FirstOrDefault();
                response.PageVisits.Add(new SurveyPageVisit { PageId = response.CurrentPageId, EnteredUtc = now });
                response.LastActivityUtc = now;
                response.ModifiedUtc = now;
                await _responseStore.UpdateAsync(response, cancellationToken);

                // Commit while the completion lock is held, so a parallel completion sees the released counts.
                await _session.SaveChangesAsync(cancellationToken);
            }

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
            await GiveBackParticipationAsync(survey, version, response, cancellationToken);
            await _responseStore.DeleteAsync(response, cancellationToken);

            // Every file of the response goes with it, once the delete is committed.
            notifications.Add(() => Files.DeleteAllAsync(response, CancellationToken.None));

            var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response, ActorId = actorId };
            notifications.Add(() => NotifyAsync(observer => observer.DeletedAsync(context)));

            return new SurveyResponseResult { Response = response };
        }, cancellationToken, allowMissingVersion: true);

    // A response staff delete gives its attempt back to an identified respondent: the start and the counted completion it
    // added to their participation are taken back, as the count of their kept responses drops (the limits read the larger
    // of the two). A guest's or an anonymous respondent's participation can't be tied to one response, so it stays.
    private async Task GiveBackParticipationAsync(Survey survey, SurveyVersion version, SurveyResponse response, CancellationToken cancellationToken)
    {
        if (response.IsTest || string.IsNullOrEmpty(response.UserId) || SurveyAnonymity.IsAnonymous(survey))
        {
            return;
        }

        var invitation = string.IsNullOrEmpty(response.InvitationId) ? null : await _invitationStore.FindByIdAsync(response.InvitationId, cancellationToken);
        var respondentKey = await CompletionRespondentKeyAsync(survey, response, identity: null, invitation, cancellationToken);

        // A completion is counted when the response completed (a voided one was voided after it completed); a screened-out
        // response's never was.
        var screenedOut = response.Status == SurveyResponseStatus.ScreenedOut
            || version?.Definition?.Endings?.FirstOrDefault(ending => string.Equals(ending?.Id, response.EndingId, StringComparison.Ordinal))?.Outcome == EndingOutcome.ScreenedOut;
        var counted = response.CompletedUtc is not null
            && response.Status is SurveyResponseStatus.Completed or SurveyResponseStatus.Voided
            && !screenedOut;

        await _participation.RecordDeletedAsync(survey, respondentKey, response.Context, response.StartedUtc, counted ? response.CompletedUtc : null);
    }

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
            response.RespondentKey = null;
            response.Accommodation = null;
            response.ModifiedUtc = _clock.UtcNow;
            await _responseStore.UpdateAsync(response, cancellationToken);

            var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response };
            notifications.Add(() => NotifyAsync(observer => observer.AbandonedAsync(context)));

            return new SurveyResponseResult { Response = response };
        }, cancellationToken, allowMissingVersion: true);

    /// <summary>
    /// Counts one more failed upkeep run on a response, under its write lock, for the maintenance task.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, with the response and its new <see cref="SurveyResponse.UpkeepFailures"/>.</returns>
    public Task<SurveyResponseResult> CountUpkeepFailureAsync(string responseId, CancellationToken cancellationToken = default)
        => WriteAsync<SurveyResponseResult>(responseId, () => new SurveyResponseResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            response.UpkeepFailures++;
            await _responseStore.UpdateAsync(response, cancellationToken);

            return new SurveyResponseResult { Response = response };
        }, cancellationToken, allowMissingVersion: true);

    /// <summary>
    /// Clears the count of failed upkeep runs of a response, under its write lock: after an upkeep step succeeds, or when
    /// an admin retries a response upkeep had stopped trying.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<SurveyResponseResult> ClearUpkeepFailuresAsync(string responseId, CancellationToken cancellationToken = default)
        => WriteAsync<SurveyResponseResult>(responseId, () => new SurveyResponseResult { Error = SurveyErrorCode.Busy }, async (survey, version, response, notifications) =>
        {
            if (response.UpkeepFailures != 0)
            {
                response.UpkeepFailures = 0;
                await _responseStore.UpdateAsync(response, cancellationToken);
            }

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

    private SurveyResponseFiles Files
        => _serviceProvider.GetRequiredService<SurveyResponseFiles>();

    // An anonymous response keeps a context only when its kind can't point to one person; otherwise its kind only.
    private RecordRef? StoredContext(Survey survey, RecordRef? context)
        => SurveyAnonymity.StoredContext(survey, context, _serviceProvider.GetService<IOptions<SurveyContextKindOptions>>()?.Value);

    // Finds the invitation an anonymous response was started from. The invitation's index keeps an HMAC of its seal keyed
    // by the survey's protected secret, so one indexed query finds it, and nothing stored links the two without the
    // secret. Invitations made before that value was kept are still matched by recomputing their seals.
    private async Task<SurveyInvitation> FindSealedInvitationAsync(Survey survey, SurveyResponse response, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(response.CollectorId))
        {
            return null;
        }

        var secret = await Secrets.RevealAsync(survey, cancellationToken);
        var lookup = SurveyRespondentKeys.InvitationSealLookup(secret, response.InvitationSeal);
        var found = await _invitationStore.FindBySealLookupAsync(response.CollectorId, lookup, cancellationToken);

        if (found is not null)
        {
            return found.Status is SurveyInvitationStatus.Pending or SurveyInvitationStatus.Sent or SurveyInvitationStatus.Opened or SurveyInvitationStatus.Started or SurveyInvitationStatus.Completed
                ? found
                : null;
        }

        var legacy = await _invitationStore.ListWithoutSealLookupAsync(
            response.CollectorId,
            [SurveyInvitationStatus.Pending, SurveyInvitationStatus.Sent, SurveyInvitationStatus.Opened, SurveyInvitationStatus.Started, SurveyInvitationStatus.Completed],
            cancellationToken);

        return legacy.FirstOrDefault(candidate => string.Equals(SurveyRespondentKeys.InvitationSeal(secret, candidate.ItemId), response.InvitationSeal, StringComparison.Ordinal));
    }

    private async Task<SurveyResponse> FindOpenAsync(Survey survey, StartResponseRequest request, SurveyRespondentIdentity identity, SurveyInvitation invitation, bool anonymous, bool isTest, CancellationToken cancellationToken)
    {
        // Opened through a personal invitation, only a response of that invitation resumes: a response the same browser or
        // user started through the public link stays apart, so the invitation completes with its own response.
        string seal = null;

        if (invitation is not null && anonymous && !isTest)
        {
            seal = SurveyRespondentKeys.InvitationSeal(await Secrets.RevealAsync(survey, cancellationToken), invitation.ItemId);
        }

        bool OfInvitation(SurveyResponse response)
            => invitation is null
                || (anonymous
                    ? string.Equals(response.InvitationSeal, seal, StringComparison.Ordinal)
                    : string.Equals(response.InvitationId, invitation.ItemId, StringComparison.Ordinal));

        if (!string.IsNullOrEmpty(identity.ResumeToken))
        {
            var byToken = await FindForRespondentAsync(SurveyResumeTokens.ResponseIdOf(identity.ResumeToken), identity, cancellationToken);

            if (byToken is { Status: SurveyResponseStatus.InProgress }
                && string.Equals(byToken.SurveyId, survey.ItemId, StringComparison.Ordinal)
                && byToken.IsTest == isTest
                && Nullable.Equals(byToken.Context, StoredContext(survey, request.Context))
                && (isTest || OfInvitation(byToken)))
            {
                return byToken;
            }
        }

        if (!anonymous && !isTest && !string.IsNullOrEmpty(identity.UserId)
            && await _counter.FindInProgressAsync(survey.ItemId, identity.UserId, request.Context?.ToString()) is { } byUser
            && OfInvitation(byUser))
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

        // Every attempt the respondent started counts, a voided one included: voiding doesn't give an attempt back, so the
        // next attempt never takes the number of a voided one.
        if (!string.IsNullOrEmpty(response.UserId))
        {
            return await _counter.CountAttemptsUsedByUserAsync(survey.ItemId, response.UserId, request.Context?.ToString(), countStarted: true) + 1;
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

    // Reads the answers of the current screen. In Draft mode no file is kept; in Commit mode the new files are collected
    // in pendingFiles, by question, for the caller to store once the page passes validation.
    private async Task<Dictionary<string, List<string>>> ReadScreenAsync(SurveyDefinition definition, SurveyVersion version, SurveyResponse response, SubmitPageRequest request, SurveyAnswerReadMode mode, Dictionary<string, List<SurveyPendingFile>> pendingFiles, CancellationToken cancellationToken)
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
                await ReadQuestionAsync(definition, response, before, question, request, errors, mode, pendingFiles, cancellationToken);
            }
        }

        return errors;
    }

    private async Task ReadQuestionAsync(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, SurveyQuestion question, SubmitPageRequest request, Dictionary<string, List<string>> errors, SurveyAnswerReadMode mode, Dictionary<string, List<SurveyPendingFile>> pendingFiles, CancellationToken cancellationToken)
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
            Mode = pendingFiles is null ? SurveyAnswerReadMode.Draft : mode,
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

        if (context.PendingFiles.Count > 0)
        {
            if (pendingFiles is null)
            {
                // Nothing may be kept: a file a handler read anyway is dropped from the answer.
                context.Answer?.AttachmentIds?.RemoveAll(SurveyResponseFiles.IsPending);
            }
            else
            {
                pendingFiles[question.Id] = [.. context.PendingFiles];
            }
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
        // resolved the chain of rules. An answer checked with immediate feedback is locked and never dropped or changed,
        // so hiding its question for a moment can't erase it to answer again, or strip a wrong pick from it.
        foreach (var questionId in response.Answers.Keys.ToList())
        {
            if (response.Answers[questionId]?.CheckedUtc is not null)
            {
                continue;
            }

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

        // The pattern rules of every answer checked here share one time budget, so many pattern questions can't
        // multiply the time one request may take.
        var patternBudget = new SurveyPatternBudget(SurveyPatternBudget.DefaultPerRequest);

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

            // A carried-forward question with nothing carried forward shows no option, so it can't be required.
            var required = state.RequiredQuestionIds.Contains(questionId) && !SurveyPageViewBuilder.NothingCarriedForward(definition, question, response, state);
            var context = new SurveyAnswerValidationContext
            {
                Question = question,
                Answer = answer,
                IsRequired = required,
                Definition = definition,
                Response = response,
                AllowedChoiceIds = SurveyPageViewBuilder.AllowedChoices(definition, question, response, state),
                PatternBudget = patternBudget,
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

    private static bool CanGoBack(SurveyDefinition definition, SurveyResponse response, DateTime utcNow)
    {
        if (response.PagePath.Count < 2 || definition.Navigation?.AllowBack == false || definition.Navigation?.Sequential == true)
        {
            return false;
        }

        var pageId = SurveyPlanNavigator.PageOf(response.Plan, response.CurrentPageId);
        var page = definition.Pages?.FirstOrDefault(item => string.Equals(item.Id, pageId, StringComparison.Ordinal));

        // A timed page whose time is up can't be opened again: its answers can no longer change.
        return page?.AllowBack != false
            && !SurveyPageTimer.ScreenTimeIsUp(definition, response, response.PagePath[response.PagePath.Count - 2], utcNow);
    }

    private (string Next, SurveyEnding Ending) NextScreen(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, string cacheKey)
    {
        var step = ChooseNext(definition, response, state, cacheKey, response.CurrentPageId);

        // The answers of the pages a branch skips are dropped, except the locked answers checked with immediate feedback.
        SkipScreens(response, step);

        // Only a branch names an ending here. Otherwise the completion chooses it once the response is scored, so an
        // ending (or a rule it reads) on the score sees this response's score.
        return (step.Next, step.Ending);
    }

    // Drops the answers of the screens a branch skips, except the locked ones.
    private static void SkipScreens(SurveyResponse response, NextStep step)
    {
        for (var skipped = step.SkipFrom; skipped < step.SkipTo; skipped++)
        {
            RemoveUnlocked(response, SurveyPlanNavigator.QuestionsOf(response.Plan, response.Plan.PageOrder[skipped]));
        }
    }

    // Chooses the screen after a screen, as a page post forward does: a branch that holds on the last screen of a page,
    // else the next screen that shows. It changes nothing: the caller drops the answers of the screens a branch skips.
    private NextStep ChooseNext(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, string cacheKey, string current)
    {
        var plan = response.Plan;
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
                        return new NextStep(null, ending, 0, 0);
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

                return new NextStep(target, null, index + 1, target is null ? plan.PageOrder.Count : plan.PageOrder.IndexOf(target));
            }
        }

        return new NextStep(SurveyPlanNavigator.FirstShowing(plan, state, index + 1), null, 0, 0);
    }

    // The screen after a screen (null at the end), the ending a branch goes to, and the plan positions of the screens a
    // branch skips (SkipFrom up to, not including, SkipTo).
    private readonly record struct NextStep(string Next, SurveyEnding Ending, int SkipFrom, int SkipTo);

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

            var respondentKey = await CompletionRespondentKeyAsync(survey, response, identity, invitation, cancellationToken);

            // The same completion rules decide on every path: a respondent's submit, and a submission for them at the
            // deadline or once the survey closed. Without the respondent's request (the deadline task) the identity is
            // the response's own user, so a rule about who responds still decides, and the accommodation is the user's,
            // else the one the start granted (an anonymous response keeps no user).
            var decision = await _access.CheckCompleteAsync(new SurveyAccessContext
            {
                Survey = survey,
                Version = version,
                Collector = string.IsNullOrEmpty(response.CollectorId) ? null : await _collectorStore.FindByIdAsync(response.CollectorId, cancellationToken),
                Invitation = invitation,
                Identity = identity ?? new SurveyRespondentIdentity { UserId = response.UserId },
                RespondentKey = respondentKey,
                Context = response.Context,
                Response = response,
                Override = await OverrideForAsync(survey, response, identity, cancellationToken),
                UtcNow = now,
                IsTest = response.IsTest,
                AutoSubmitted = autoSubmitted,
            });

            if (!decision.IsAllowed)
            {
                if (!autoSubmitted)
                {
                    // The respondent's submit is refused and the response stays in progress, with the reason shown.
                    return decision.Error;
                }

                // Nobody is there to be told why, and the response can't stay in progress past its deadline or the close:
                // it ends abandoned, neither scored nor counted, with its answers kept for staff.
                await AbandonRefusedAsync(survey, version, response, now, notifications, cancellationToken);

                return SurveyErrorCode.None;
            }

            LeaveScreen(response, now);

            if (!autoSubmitted)
            {
                // The respondent submitted from the end of their path: nothing on it is left unreached.
                response.UnreachedPagePath = [];
            }

            // The response is being completed: from here it is scored as completed, so the engine counts the pages on
            // the respondent's path (not the pages a branch skipped), the same questions a regrade and the review count.
            // The final status (completed or screened out) is set once the ending is chosen below.
            response.Status = SurveyResponseStatus.Completed;

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

            // Unless a branch named it, the ending is chosen now, after scoring, so a rule on the score reads this score.
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

            // The completion is counted: the response no longer keeps who it was counted for, nor what their
            // accommodation granted (on an anonymous survey, that could single the person out).
            response.RespondentKey = null;
            response.Accommodation = null;

            var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response };

            await NotifyAsync(observer => observer.CompletingAsync(context));
            await _responseStore.UpdateAsync(response, cancellationToken);

            // Commit while the completion lock is held, so a parallel completion counts this one.
            await _session.SaveChangesAsync(cancellationToken);
            Files.MarkCommitted();

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

    // The key a completion counts under: the key its start was counted under, kept on the response, so a completion
    // with no respondent's request (the deadline task) still counts toward the person's limits. A response started
    // before the key was kept uses the caller's identity, or else rebuilds the key from its user or invitation.
    private async Task<string> CompletionRespondentKeyAsync(Survey survey, SurveyResponse response, SurveyRespondentIdentity identity, SurveyInvitation invitation, CancellationToken cancellationToken)
    {
        if (response.IsTest)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(response.RespondentKey))
        {
            return response.RespondentKey;
        }

        if (identity is not null && await Secrets.RespondentKeyAsync(survey, identity, invitation, cancellationToken) is { Length: > 0 } key)
        {
            return key;
        }

        if (string.IsNullOrEmpty(response.UserId) && invitation is null)
        {
            return null;
        }

        return await Secrets.RespondentKeyAsync(survey, new SurveyRespondentIdentity { UserId = response.UserId }, invitation, cancellationToken);
    }

    // Ends a response whose submission for the respondent the completion rules refused (a limit reached, the most
    // responses taken, a used invitation, a full quota that closes the survey, a learner withdrawn): it is abandoned,
    // not scored and not counted, and its answers stay for staff. Runs under the completion lock.
    private async Task AbandonRefusedAsync(Survey survey, SurveyVersion version, SurveyResponse response, DateTime now, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        LeaveScreen(response, now);

        response.Status = SurveyResponseStatus.Abandoned;
        response.AutoSubmitted = true;
        response.RespondentKey = null;
        response.Accommodation = null;
        response.ModifiedUtc = now;
        await _responseStore.UpdateAsync(response, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
        Files.MarkCommitted();

        var context = new SurveyResponseEventContext { Survey = survey, Version = version, Response = response };
        notifications.Add(() => NotifyAsync(observer => observer.AbandonedAsync(context)));
    }

    // Submits a response for its respondent, at its deadline or once its survey closed, with the answers it had saved.
    // Returns Busy when the completion lock was held (nothing changed), else None.
    private async Task<SurveyErrorCode> SubmitExpiredAsync(Survey survey, SurveyVersion version, SurveyResponse response, SurveyRespondentIdentity identity, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        var definition = version.Definition;
        var cacheKey = SurveyRuntimeVersions.CacheKey(version);
        var state = Settle(definition, response, cacheKey);

        // Autosave keeps answers without validating them: an answer that breaks its question's rules (too many choices,
        // a number out of range...) is dropped rather than scored. A checked answer was validated when it was checked.
        var notChecked = response.Answers.Where(pair => pair.Value?.CheckedUtc is null).Select(pair => pair.Key).ToList();
        var invalid = await ValidateQuestionsAsync(definition, response, state, notChecked, new Dictionary<string, List<string>>(StringComparer.Ordinal));

        if (invalid.Count > 0)
        {
            RemoveUnlocked(response, invalid.Keys);
            state = Settle(definition, response, cacheKey);
        }

        // The pages still ahead on the respondent's path count toward the score with 0 points, so walking away from a
        // timed or closing quiz can't pass it on the pages answered so far. Pages a branch skips stay out.
        response.UnreachedPagePath = UnreachedScreens(definition, response, state, cacheKey);

        // The completion chooses the ending once the response is scored.
        return await CompleteCoreAsync(survey, version, response, ending: null, autoSubmitted: true, identity, notifications, cancellationToken);
    }

    // Walks on from the screen the respondent is on to the end, choosing each next screen as a page post forward would with
    // the answers given so far: a branch whose rule holds on those answers is taken (and drops the answers of the screens
    // it skips, unless locked), a branch point left unanswered takes no branch, so the walk goes on to the next screen
    // that shows, and a branch to an ending ends it. Returns the screens the walk reached, in order.
    private List<string> UnreachedScreens(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, string cacheKey)
    {
        var unreached = new List<string>();
        var plan = response.Plan;

        if (plan?.PageOrder is not { Count: > 0 } || string.IsNullOrEmpty(response.CurrentPageId) || !plan.PageOrder.Contains(response.CurrentPageId))
        {
            return unreached;
        }

        var seen = new HashSet<string>(response.PagePath ?? [], StringComparer.Ordinal) { response.CurrentPageId };
        var screen = response.CurrentPageId;

        // A next screen is always further on in the plan, so the walk ends; the set stops it on a bad plan too.
        while (screen is not null)
        {
            var step = ChooseNext(definition, response, state, cacheKey, screen);

            if (step.SkipTo > step.SkipFrom)
            {
                SkipScreens(response, step);
                state = Settle(definition, response, cacheKey);
            }

            if (step.Next is null || !seen.Add(step.Next))
            {
                break;
            }

            unreached.Add(step.Next);
            screen = step.Next;
        }

        return unreached;
    }

    // Takes back what completing a response counted: the respondent's participation and the quotas it matched. Runs
    // under the survey's completion lock.
    private async Task ReleaseCompletionAsync(Survey survey, SurveyVersion version, SurveyResponse response, SurveyRespondentIdentity identity, CancellationToken cancellationToken)
    {
        if (response.IsTest)
        {
            return;
        }

        var definition = version.Definition;

        // A response submitted at its deadline counts toward the respondent's limits too, so it is taken back the same
        // way (nothing is taken back when that day counts no completion for the respondent).
        if (identity is not null && response.CompletedUtc is { } completedUtc)
        {
            var invitation = string.IsNullOrEmpty(response.InvitationId) ? null : await _invitationStore.FindByIdAsync(response.InvitationId, cancellationToken);
            var respondentKey = await Secrets.RespondentKeyAsync(survey, identity, invitation, cancellationToken);

            await _participation.RecordReopenedAsync(survey, respondentKey, response.Context, completedUtc);

            // In progress again: a completion at the deadline counts under the same key, with the same accommodation.
            response.RespondentKey = respondentKey;

            if (!string.IsNullOrEmpty(identity.UserId) && _serviceProvider.GetService<ISurveyOverrideService>() is { } overrides)
            {
                response.Accommodation = AccommodationOf(await overrides.FindEffectiveAsync(survey.ItemId, identity.UserId, response.Context, cancellationToken));
            }
        }

        if (definition.Quotas is { Count: > 0 } && _serviceProvider.GetService<ISurveyQuotaService>() is { } quotas)
        {
            var state = _logic.Evaluate(definition, response, SurveyRuntimeVersions.CacheKey(version));

            await quotas.ReleaseAsync(survey, version, response, state, cancellationToken);
        }
    }

    // Copies of the answers that keep stored files, by question id.
    private static Dictionary<string, SurveyAnswer> StoredFileAnswers(SurveyResponse response)
    {
        var stored = new Dictionary<string, SurveyAnswer>(StringComparer.Ordinal);

        foreach (var (questionId, answer) in response.Answers)
        {
            if (answer?.AttachmentIds?.Any(id => !string.IsNullOrEmpty(id) && !SurveyResponseFiles.IsPending(id)) == true)
            {
                stored[questionId] = JsonSerializer.Deserialize<SurveyAnswer>(JsonSerializer.Serialize(answer, _cloneOptions), _cloneOptions);
            }
        }

        return stored;
    }

    // Puts back the stored answer of each question that kept files before a refused post.
    private static void RestoreFileAnswers(SurveyResponse response, IEnumerable<string> questionIds, Dictionary<string, SurveyAnswer> stored)
    {
        foreach (var questionId in questionIds)
        {
            if (stored.TryGetValue(questionId, out var answer) && response.Answers.GetValueOrDefault(questionId)?.CheckedUtc is null)
            {
                response.Answers[questionId] = answer;
            }
        }
    }

    // Removes answers, keeping every answer checked with immediate feedback: a checked answer is locked for good.
    private static void RemoveUnlocked(SurveyResponse response, IEnumerable<string> questionIds)
    {
        foreach (var questionId in questionIds.ToList())
        {
            if (response.Answers.TryGetValue(questionId, out var answer) && answer?.CheckedUtc is not null)
            {
                continue;
            }

            response.Answers.Remove(questionId);
        }
    }

    // Refuses a write to a response that ended, and submits one whose deadline and grace passed, or whose survey closed,
    // with the answers saved before: nothing posted now is read. The identity is the respondent's own, so a late
    // completion still counts toward their limits.
    private async Task<SurveyErrorCode?> RefuseClosedAsync(Survey survey, SurveyVersion version, SurveyResponse response, SurveyRespondentIdentity identity, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        if (response.Status != SurveyResponseStatus.InProgress)
        {
            return SurveyErrorCode.InvalidState;
        }

        var grace = SurveyAvailability.GraceSeconds(version.Definition);
        var now = _clock.UtcNow;

        if (response.DeadlineUtc is { } deadline && now >= deadline.AddSeconds(grace))
        {
            return await SubmitExpiredAsync(survey, version, response, identity, notifications, cancellationToken) == SurveyErrorCode.Busy
                ? SurveyErrorCode.Busy
                : SurveyErrorCode.DeadlinePassed;
        }

        // The closing date gets the same grace as the deadline (a deadline is often clamped to it): a last page the timer
        // or the respondent posted just before the close, and still in flight at it, is taken with its answers. A close by
        // staff (the survey's or the link's status) takes effect at once.
        if (await ClosedForAsync(survey, response, identity, now.AddSeconds(-grace), cancellationToken))
        {
            return await SubmitExpiredAsync(survey, version, response, identity, notifications, cancellationToken) == SurveyErrorCode.Busy
                ? SurveyErrorCode.Busy
                : SurveyErrorCode.Closed;
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

            await _formulas.PrepareAsync();
            var survey = await _surveyStore.FindByIdAsync(response.SurveyId, cancellationToken);
            var version = await _versions.GetAsync(survey, response, cancellationToken);

            if (survey is null || (version?.Definition is null && !allowMissingVersion))
            {
                return new T { Error = SurveyErrorCode.NotFound, Response = response };
            }

            // A file stored during the request and not yet committed is removed when the request fails: its record rolls
            // back with the session, so neither the response nor the orphan sweep would ever find the file again.
            try
            {
                result = await work(survey, version, response, notifications);
            }
            catch
            {
                await Files.DiscardUncommittedAsync();

                throw;
            }

            try
            {
                await _session.SaveChangesAsync(cancellationToken);
                Files.MarkCommitted();
            }
            catch (ConcurrencyException)
            {
                notifications.Clear();
                result = busy();
                await Files.DiscardUncommittedAsync();
            }
            catch
            {
                await Files.DiscardUncommittedAsync();

                throw;
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
