using System.Buffers;
using System.Net.Mail;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Creates personal invitations and their links, sends them and their reminders.
/// </summary>
public sealed class SurveyInvitationService : ISurveyInvitationService
{
    /// <summary>
    /// The start of the key of a list's notification lock; the list's id follows.
    /// </summary>
    public const string NotificationLockPrefix = "Surveys:InvitationNotifications:";

    private static readonly SurveyInvitationStatus[] _remindable =
    [
        SurveyInvitationStatus.Sent,
        SurveyInvitationStatus.Opened,
        SurveyInvitationStatus.Started,
    ];

    // Characters that never belong in one plain address: list separators, white space, display-name syntax.
    private static readonly SearchValues<char> _notInAddress = SearchValues.Create(",; \t\r\n<>\"");

    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyInvitationStore _invitationStore;
    private readonly SurveyInvitationLinks _links;
    private readonly SurveyParticipationStore _participationStore;
    private readonly IEnumerable<ISurveyInvitationSender> _senders;
    private readonly IEnumerable<ISurveyInvitationRecipientPolicy> _recipientPolicies;
    private readonly SurveyAnonymitySecrets _secrets;
    private readonly IClock _clock;
    private readonly SurveyInvitationEmailLimit _emailLimit;
    private readonly IDistributedLock _distributedLock;
    private readonly ILogger _logger;

    // The e-mails of this scope the mail server failed, whose attempts are counted when the scope's e-mail work ends. A
    // failure is confirmed as its address's when an e-mail of the scope went out after it: the mail server was taking
    // e-mails then, so it uses up an attempt. A failure no e-mail went out after also counts when an earlier e-mail of
    // the scope went out and the send didn't stop as in an outage (one address tried last, refused). Only the failures
    // in a row that stopped the send (an outage or a throttle that started partway through), or the failures of a scope
    // that sent nothing, can't be told from an outage: each uses up an attempt by the lone-failure rule (R8-M2, R9-M2).
    private readonly List<(string ItemId, bool IsReminder, bool Confirmed)> _unconfirmedFailures = [];

    // How many e-mails in a row the mail server failed; at two the scope stops sending, as in an outage.
    private int _failuresInARow;

    // Whether the send commits as it goes (a bulk send of its own scope: Invite many, Send, reminders), so respondents
    // never wait on it and what it sent survives a later failure or a cancel (R8-M3). A single invite runs in its
    // caller's transaction (a workflow, the API) and leaves the commit to it.
    private bool _commitsAsItGoes;

    // Asked only between commits of a bulk send: once set, the send stops cleanly and keeps what it committed.
    private CancellationToken _stopToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationService"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="invitationStore">The invitation store.</param>
    /// <param name="links">The link builder.</param>
    /// <param name="participationStore">The participation store, which tells when an anonymous invitation was answered.</param>
    /// <param name="senders">The invitation sender, when the module registers one.</param>
    /// <param name="recipientPolicies">The opt-out and consent checks asked before an e-mail invitation is sent.</param>
    /// <param name="secrets">The anonymity secrets, which sign the invitation links.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="emailLimit">The site's daily e-mail limit, held under the tenant's invitation e-mail lock.</param>
    /// <param name="distributedLock">The tenant's lock service, for the lock a list's notifications to users are sent under.</param>
    /// <param name="logger">The logger.</param>
    public SurveyInvitationService(
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        SurveyInvitationStore invitationStore,
        SurveyInvitationLinks links,
        SurveyParticipationStore participationStore,
        IEnumerable<ISurveyInvitationSender> senders,
        IEnumerable<ISurveyInvitationRecipientPolicy> recipientPolicies,
        SurveyAnonymitySecrets secrets,
        IClock clock,
        SurveyInvitationEmailLimit emailLimit,
        IDistributedLock distributedLock,
        ILogger<SurveyInvitationService> logger)
    {
        _emailLimit = emailLimit;
        _distributedLock = distributedLock;
        _logger = logger;
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _invitationStore = invitationStore;
        _links = links;
        _participationStore = participationStore;
        _senders = senders;
        _recipientPolicies = recipientPolicies;
        _secrets = secrets;
        _clock = clock;
    }

    /// <summary>
    /// How long a list's notifications to users may go out under the list's notification lock in one send; the rest wait
    /// for the next run. Well under <see cref="SurveyInvitationEmailLimit.LockExpiration"/>, so the lock never expires
    /// while the notifications it covers are unsaved.
    /// </summary>
    public static readonly TimeSpan NotificationWindow = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Gets whether a send of this scope found another send going out (the invitation e-mail lock, or a list's
    /// notification lock, was busy past its wait) and so sent nothing of its own: the caller tells the user why.
    /// </summary>
    public bool AnotherSendIsGoingOut { get; private set; }

    /// <summary>
    /// Gets whether the mail server failed e-mails of this scope as in an outage (two in a row, and none went out), so its
    /// other e-mails wait for the hourly reminder task. When e-mails of the scope went out, the failures were the
    /// addresses' (see <see cref="RefusedEmails"/>).
    /// </summary>
    public bool MailServerFailed
        => _emailLimit.MailServerFailed && !_emailLimit.HasSent;

    /// <summary>
    /// Gets how many e-mails of this scope the mail server refused; they are tried again by the hourly reminder task.
    /// </summary>
    public int RefusedEmails { get; private set; }

    // Whether a bulk send was asked to stop (checked only between its commits).
    private bool StopRequested
        => _stopToken.IsCancellationRequested;

    /// <inheritdoc />
    /// <remarks>
    /// Every send of a scope is one batch of the daily e-mail limit (see <see cref="SurveyInvitationEmailLimit"/>), so
    /// calling this once per recipient in one scope (a workflow loop) reads the limit and asks for its lock once. To
    /// invite many recipients at once, use <see cref="InviteManyAsync"/>.
    /// </remarks>
    public async Task<SurveyInvitationResult> InviteAsync(SurveyInvitationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Another send holds the e-mail lock (R8-Mi11): the invitation is still created (and held), but an existing one is
        // left as it is, since this copy may be older than that send's marks.
        var busy = IsEmail(request) && await IsAnotherSendGoingOutAsync(cancellationToken);
        var survey = await FindLiveSurveyAsync(request.SurveyId, cancellationToken);
        var collector = survey is null ? null : await FindCollectorAsync(survey, request.CollectorId, cancellationToken);

        if (collector is null)
        {
            // Nothing was written and nothing e-mailed, so the e-mail lock this invite took is let go of at once rather
            // than held to the end of the scope (R9-Mi4). Otherwise a single invite keeps it until its scope saves: it
            // commits nothing itself (that would commit its caller's unfinished work too), and its writes, e-mailed or
            // held, must reach the database before another sender reads or counts them.
            if (!_commitsAsItGoes && !_emailLimit.HasSent)
            {
                await _emailLimit.ReleaseAsync();
            }

            return new SurveyInvitationResult { Error = SurveyErrorCode.NotFound };
        }

        var result = await CreateCoreAsync(survey, collector, request, requireRecipient: true, mayChangeExisting: !busy, cancellationToken);

        if (result.Succeeded && result.Invitation.Status == SurveyInvitationStatus.Pending)
        {
            result.Invitation = (await DeliverAsync(survey, collector, result.Invitation, result.Link, isReminder: false, cancellationToken)).Invitation;
            await ResolveFailuresAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Invites many recipients to one invitation collector as one send: every invitation is created first, then, when
    /// <paramref name="send"/> is set, the new ones are sent in one batch of the daily e-mail limit. The limit is read and
    /// its lock asked for once; when the lock is busy, the limit is reached or the batch's sending window passes, the
    /// remaining e-mails are held, without waiting again, and the hourly reminder task sends them.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="collectorId">The invitation collector, or <see langword="null"/> for the survey's personal links collector.</param>
    /// <param name="requests">Who to invite; their survey and collector are set from the parameters.</param>
    /// <param name="send">Whether to send the invitations, or only create them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result per request, in order.</returns>
    public async Task<IReadOnlyList<SurveyInvitationResult>> InviteManyAsync(string surveyId, string collectorId, IReadOnlyCollection<SurveyInvitationRequest> requests, bool send, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        // Another send holds the e-mail lock (R8-Mi11): new invitations are still created (and held), but existing ones
        // are left as they are, since these copies may be older than that send's marks; their results say Busy.
        var busy = send && requests.Any(request => request is not null && IsEmail(request)) && await IsAnotherSendGoingOutAsync(cancellationToken);
        var survey = await FindLiveSurveyAsync(surveyId, cancellationToken);
        var collector = survey is null ? null : await FindCollectorAsync(survey, collectorId, cancellationToken);
        var results = new List<SurveyInvitationResult>(requests.Count);

        foreach (var request in requests)
        {
            ArgumentNullException.ThrowIfNull(request);

            request.SurveyId = surveyId;
            request.CollectorId = collector?.ItemId ?? collectorId;

            results.Add(collector is null
                ? new SurveyInvitationResult { Error = SurveyErrorCode.NotFound }
                : await CreateCoreAsync(survey, collector, request, requireRecipient: true, mayChangeExisting: !busy, cancellationToken));
        }

        if (!send || collector is null)
        {
            return results;
        }

        // The lock was taken before the invitations were created, so existing ones are read after every other sender
        // saved; the sending window starts now, so creating up to 5,000 invitations doesn't use it up (R7-Mi11).
        _emailLimit.StartSendingWindow();

        // From here the send commits as it goes and runs to its end, or to a clean stop between commits (R8-M3); the
        // first commit also commits the invitations just created.
        BeginBulkSend(cancellationToken);

        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new List<SurveyInvitation>();

        foreach (var result in results)
        {
            if (result.Succeeded && result.Invitation.Status == SurveyInvitationStatus.Pending && links.TryAdd(result.Invitation.ItemId, result.Link))
            {
                pending.Add(result.Invitation);
            }
        }

        var (_, current) = await DeliverFirstSendsAsync(survey, collector, pending, invitation => links[invitation.ItemId]);

        // The same recipient asked for twice shares one invitation; every result gets the copy as it stands now.
        foreach (var result in results)
        {
            if (result.Invitation is not null && current.TryGetValue(result.Invitation.ItemId, out var now))
            {
                result.Invitation = now;
            }
        }

        await EndEmailWorkAsync();

        return results;
    }

    /// <summary>
    /// Creates an invitation and its link without sending it, or renews the link of the recipient's open invitation.
    /// </summary>
    /// <param name="request">The invitation.</param>
    /// <param name="requireRecipient">Whether a user, a party or an e-mail address is required; a bare personal link needs none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result with the personal link.</returns>
    public async Task<SurveyInvitationResult> CreateAsync(SurveyInvitationRequest request, bool requireRecipient = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var survey = await FindLiveSurveyAsync(request.SurveyId, cancellationToken);
        var collector = survey is null ? null : await FindCollectorAsync(survey, request.CollectorId, cancellationToken);

        if (collector is null)
        {
            return new SurveyInvitationResult { Error = SurveyErrorCode.NotFound };
        }

        return await CreateCoreAsync(survey, collector, request, requireRecipient, mayChangeExisting: true, cancellationToken);
    }

    // The survey, or null when it doesn't exist or is being deleted: a survey being deleted invites and reminds nobody, so
    // no invitation is written while its records are removed.
    private async Task<Survey> FindLiveSurveyAsync(string surveyId, CancellationToken cancellationToken)
    {
        var survey = string.IsNullOrEmpty(surveyId) ? null : await _surveyStore.FindByIdAsync(surveyId, cancellationToken);

        return survey?.DeletingUtc is null ? survey : null;
    }

    // The survey's invitation collector by id, or its personal links collector; null when the id names another kind of
    // collector or another survey's.
    private async Task<SurveyCollector> FindCollectorAsync(Survey survey, string collectorId, CancellationToken cancellationToken)
    {
        var collector = string.IsNullOrEmpty(collectorId)
            ? await EnsureCollectorAsync(survey, cancellationToken)
            : await _collectorStore.FindByIdAsync(collectorId, cancellationToken);

        return collector is null || collector.SurveyId != survey.ItemId || collector.Kind != SurveyCollectorKinds.Invitation ? null : collector;
    }

    // mayChangeExisting: false while another send holds the e-mail lock; an existing invitation is then returned unchanged
    // with SurveyErrorCode.Busy, and only new ones are created.
    private async Task<SurveyInvitationResult> CreateCoreAsync(Survey survey, SurveyCollector collector, SurveyInvitationRequest request, bool requireRecipient, bool mayChangeExisting, CancellationToken cancellationToken)
    {
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        // One personal link goes to one address: a list (a@x;b@y) or a malformed value is refused here, below every
        // caller (the admin page, workflows, the API).
        if (email is not null && !IsSingleEmailAddress(email))
        {
            return new SurveyInvitationResult { Error = SurveyErrorCode.InvitationInvalid };
        }

        var hasRecipient = !string.IsNullOrEmpty(request.UserId) || request.Recipient is not null || email is not null;

        if (requireRecipient && !hasRecipient)
        {
            return new SurveyInvitationResult { Error = SurveyErrorCode.InvitationInvalid };
        }

        var existing = hasRecipient ? await FindForRecipientAsync(collector.ItemId, request.UserId, request.Recipient, email, cancellationToken) : null;

        if (existing is not null && existing.Status == SurveyInvitationStatus.Completed)
        {
            return new SurveyInvitationResult { Error = SurveyErrorCode.InvitationUsed, Invitation = existing };
        }

        if (existing is not null && !mayChangeExisting)
        {
            return new SurveyInvitationResult { Error = SurveyErrorCode.Busy, Invitation = existing };
        }

        var now = _clock.UtcNow;
        var settings = Settings(collector);

        void Apply(SurveyInvitation invitation)
        {
            invitation.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? invitation.DisplayName ?? email : request.DisplayName.Trim();
            invitation.ExpiresUtc = request.ExpiresUtc ?? (settings.ExpiresAfterDays is > 0 ? now.AddDays(settings.ExpiresAfterDays.Value) : invitation.ExpiresUtc);

            if (invitation.Status == SurveyInvitationStatus.Expired && (invitation.ExpiresUtc is null || invitation.ExpiresUtc > now))
            {
                invitation.Status = SurveyInvitationStatus.Pending;
            }

            // Inviting again retries an invitation that bounced or whose address had opted out: it is pending again, and
            // the sender and the opt-out checks decide again when it is sent.
            if (invitation.Status is SurveyInvitationStatus.Bounced or SurveyInvitationStatus.OptedOut)
            {
                invitation.Status = SurveyInvitationStatus.Pending;
                invitation.Properties.Remove(nameof(SurveyInvitationHold));
            }

            // Inviting again also gives reminders that failed for good another chance.
            invitation.Properties.Remove(nameof(SurveyInvitationReminderFailures));

            if (request.Context is { IsEmpty: false } context)
            {
                invitation.Put(new SurveyInvitationContext { Context = context.ToString() });
            }
        }

        if (existing is null)
        {
            var created = new SurveyInvitation
            {
                ItemId = IdGenerator.GenerateId(),
                SurveyId = survey.ItemId,
                CollectorId = collector.ItemId,
                UserId = request.UserId,
                Recipient = request.Recipient,
                Email = email,
                Status = SurveyInvitationStatus.Pending,
                CreatedUtc = now,
            };

            Apply(created);

            var token = await IssueTokenAsync(survey, created, cancellationToken);

            await _invitationStore.CreateAsync(created, cancellationToken);

            return new SurveyInvitationResult { Invitation = created, Link = await _links.BuildAsync(token) };
        }

        // A re-invite writes its fields on the current copy, not on the one it found (R9-Mi2): a completion or an open
        // committed since must not be written back over. A completion committed meanwhile makes it InvitationUsed, as
        // one found completed is.
        var issued = await IssueAsync(survey, existing.ItemId, cancellationToken);
        var used = false;
        var current = await _invitationStore.UpdateCurrentAsync(existing, invitation =>
        {
            if (invitation.Status == SurveyInvitationStatus.Completed)
            {
                used = true;

                return false;
            }

            Apply(invitation);
            issued.ApplyTo(invitation);

            return true;
        }, cancellationToken: cancellationToken);

        if (used)
        {
            return new SurveyInvitationResult { Error = SurveyErrorCode.InvitationUsed, Invitation = current };
        }

        return new SurveyInvitationResult { Invitation = current, Link = await _links.BuildAsync(issued.Token) };
    }

    /// <summary>
    /// Sends the invitations of a collector that were never sent.
    /// </summary>
    /// <param name="collectorId">The invitation collector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were sent.</returns>
    public async Task<int> SendPendingAsync(string collectorId, CancellationToken cancellationToken = default)
    {
        ILocker notificationLock = null;

        if (CanEmail)
        {
            // When the e-mail lock can't be had in time, another send is going out: nothing is read or written, so no
            // copy read before that send saved is written back over it (R7-Mi6), and the caller says so.
            if (!await EnterEmailSendAsync(cancellationToken))
            {
                return 0;
            }
        }
        else
        {
            // Without an e-mail service nothing takes the e-mail lock, so the list's notification lock keeps a second Send
            // from notifying the list's users again (R7-Mi7).
            notificationLock = await EnterNotificationsAsync(collectorId);

            if (notificationLock is null)
            {
                return 0;
            }
        }

        try
        {
            // Once the lock is held the send commits as it goes, and runs to its end or to a clean stop between commits
            // (R8-M3).
            BeginBulkSend(cancellationToken);

            var collector = await _collectorStore.FindByIdAsync(collectorId, CancellationToken.None);
            var survey = collector is null ? null : await FindLiveSurveyAsync(collector.SurveyId, CancellationToken.None);

            if (survey is null)
            {
                return 0;
            }

            await EnsureSecretAsync(survey);

            var invitations = await _invitationStore.ListByCollectorAsync(collector.ItemId, [SurveyInvitationStatus.Pending], SurveysConstants.PageSize, CancellationToken.None);
            var (sent, _) = await DeliverFirstSendsAsync(survey, collector, invitations, linkOf: null);

            await EndEmailWorkAsync();

            return sent;
        }
        finally
        {
            if (notificationLock is not null)
            {
                await notificationLock.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Sends the reminders that are due for a collector: invitations sent, opened or started but not completed, that
    /// have had fewer reminders than the collector allows and none for the collector's interval. First sends that were
    /// held back (see <see cref="SurveyInvitationHold"/>) are sent too, for as long as the send may e-mail: those the mail
    /// server never failed before the reminders, the others after them.
    /// <para>
    /// The e-mails go out under the tenant's invitation e-mail lock, taken only when the list has a held send or an e-mail
    /// reminder due, and before any of them is read. Their changes are committed and the lock let go of before the list's
    /// users are notified, under the list's own notification lock, so the notifications never hold up other sends nor
    /// outlive the e-mail lock, and two sends of the list never notify anyone twice.
    /// </para>
    /// </summary>
    /// <param name="collectorId">The invitation collector.</param>
    /// <param name="force">Whether to remind every open invitation now, ignoring the interval (a manual resend). An
    /// invitation already reminded today, in the site's time zone, is left out, so a resend that runs twice (a double
    /// click, or two people at once) reminds each person once.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many reminders, and invitations held back earlier, were sent.</returns>
    public async Task<int> RemindAsync(string collectorId, bool force, CancellationToken cancellationToken = default)
    {
        var collector = await _collectorStore.FindByIdAsync(collectorId, cancellationToken);
        var survey = collector is null ? null : await FindLiveSurveyAsync(collector.SurveyId, cancellationToken);

        if (survey is null || collector.Status != CollectorStatus.Open || survey.Status != SurveyStatus.Open)
        {
            return 0;
        }

        // The send commits as it goes (R8-M3): every write re-reads the invitation it changes, so no copy loaded before a
        // commit is ever saved, and the send stops cleanly, between commits, when it is cancelled.
        BeginBulkSend(cancellationToken);
        await EnsureSecretAsync(survey);

        var justSent = new HashSet<string>(StringComparer.Ordinal);
        var heldTried = new HashSet<string>(StringComparer.Ordinal);
        var settings = Settings(collector);
        var reminds = force || settings.ReminderCount > 0;
        var now = _clock.UtcNow;
        DateTime? remindedToday = force ? await _emailLimit.TodayStartUtcAsync() : null;
        DateTime? lastContactBefore = force ? null : now.AddDays(-Math.Max(settings.ReminderEveryDays, 1));
        var sent = 0;

        if (reminds)
        {
            // Expired invitations leave the open ones first, even when no reminder is due.
            await _invitationStore.VisitExpiredAsync(collector.ItemId, _remindable, now, invitation => ExpireAsync(invitation, now), SurveysConstants.PageSize, cancellationToken);
        }

        async Task RemindOneAsync(SurveyInvitation invitation)
        {
            if (justSent.Contains(invitation.ItemId))
            {
                return;
            }

            if (invitation.ExpiresUtc is { } expires && expires <= now)
            {
                await ExpireAsync(invitation, now);

                return;
            }

            if (!force && !IsReminderDue(invitation, settings, now))
            {
                return;
            }

            // A manual resend reminds an invitation at most once a day: a second resend (or one that waited for another
            // to save) finds it reminded already. So the daily limit, which keeps an invitation's last reminder time
            // only, counts every reminder.
            if (remindedToday is { } todayStart && invitation.LastReminderUtc is { } lastReminder && lastReminder >= todayStart)
            {
                return;
            }

            // An anonymous response never names its invitation, so its completion is read from the participation
            // count of the invitation's pseudonymous key instead; such an invitation gets no more reminders.
            if (await IsAnsweredAnonymouslyAsync(survey, invitation, CancellationToken.None))
            {
                await SaveCheckedAsync(invitation, current =>
                {
                    if (!_remindable.Contains(current.Status))
                    {
                        return false;
                    }

                    current.Status = SurveyInvitationStatus.Completed;

                    return true;
                }, CancellationToken.None);

                return;
            }

            if ((await DeliverAsync(survey, collector, invitation, link: null, isReminder: true, CancellationToken.None)).Sent)
            {
                sent++;
            }
        }

        // Run under a lock, so with CancellationToken.None: a cancel stops the walk between commits instead.
        async Task WalkAsync(bool emails, Func<bool> stop)
        {
            if (lastContactBefore is { } before)
            {
                // Only the invitations the index says are due are read, so a large list with nothing due costs one query.
                await _invitationStore.VisitDueForReminderAsync(collector.ItemId, _remindable, before, settings.ReminderCount, RemindOneAsync, emails, stop, SurveysConstants.PageSize, CancellationToken.None);
            }
            else
            {
                // A manual resend reminds every open invitation of the list, a page at a time.
                await _invitationStore.VisitByCollectorAsync(collector.ItemId, _remindable, RemindOneAsync, emails, stop, SurveysConstants.PageSize, CancellationToken.None);
            }
        }

        // The e-mails, under the tenant's e-mail lock, taken only when the index shows a held send or an e-mail reminder
        // due (R7-Mi11), and before any of them is read, so another send that is still going out has saved what it sent.
        if (CanEmail && await _invitationStore.HasWorkAsync(collector.ItemId, reminds ? _remindable : [], lastContactBefore, settings.ReminderCount, emails: true, cancellationToken))
        {
            if (await EnterEmailSendAsync(cancellationToken))
            {
                // First sends held back by the daily e-mail limit (or an unreadable opt-out list) go out first. Those the
                // mail server failed before go out after the reminders (R7-M6): an address it keeps refusing never stands in
                // front of the list's reminders, and by then an e-mail of the run that went out tells its failure from an
                // outage. The reminder walk ends as soon as no more e-mail of this send goes out (today's limit, the
                // sending window or a mail outage), so nothing is read or re-issued for reminders that would only wait.
                sent += await SendHeldAsync(survey, collector, justSent, heldTried, failedBefore: false);

                if (reminds)
                {
                    await WalkAsync(emails: true, () => _emailLimit.IsClosed || StopRequested);
                }

                sent += await SendHeldAsync(survey, collector, justSent, heldTried, failedBefore: true);

                // Failures counted and the rest committed, then the lock let go of, before the users are notified
                // (R7-Mi8): the next send reads what this one e-mailed, and the notifications never run under the e-mail
                // lock's lease.
                await EndEmailWorkAsync();

                if (StopRequested)
                {
                    return sent;
                }

                // The commits let go of everything the session had loaded; the survey and the list are read again.
                collector = await _collectorStore.FindByIdAsync(collectorId, CancellationToken.None);
                survey = collector is null ? null : await FindLiveSurveyAsync(collector.SurveyId, CancellationToken.None);

                if (survey is null)
                {
                    return sent;
                }
            }
        }

        if (StopRequested)
        {
            return sent;
        }

        // Then the users, whose notifications the limit doesn't count, under the list's notification lock (R7-Mi7), so a
        // second send of the list (a double click, with or without an e-mail service) notifies nobody twice: it waits for
        // this one's commits and finds its users reminded, or gives up and says another send is going out.
        if (reminds && await _invitationStore.HasWorkAsync(collector.ItemId, _remindable, lastContactBefore, settings.ReminderCount, emails: false, CancellationToken.None))
        {
            var notificationLock = await EnterNotificationsAsync(collector.ItemId);

            if (notificationLock is not null)
            {
                await using (notificationLock)
                {
                    // Read after the lock, so a send that just notified them has committed; and bounded by a window, so the
                    // walk of a large cohort ends well before the lock's lease (the rest are reminded on the next run).
                    // Each notification is committed as it goes (R8-M3).
                    var started = _clock.UtcNow;

                    await WalkAsync(emails: false, () => _clock.UtcNow - started >= NotificationWindow || StopRequested);
                    await _invitationStore.CommitAsync(CancellationToken.None);
                }
            }
        }

        return sent;
    }

    /// <summary>
    /// Gets whether an invitation is due a reminder.
    /// </summary>
    /// <param name="invitation">The invitation.</param>
    /// <param name="settings">The collector's invitation settings.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when a reminder is due.</returns>
    public static bool IsReminderDue(SurveyInvitation invitation, SurveyInvitationSettings settings, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        ArgumentNullException.ThrowIfNull(settings);

        if (!_remindable.Contains(invitation.Status) || invitation.RemindersSent >= settings.ReminderCount || SurveyInvitationReminderFailures.HaveStopped(invitation))
        {
            return false;
        }

        var last = invitation.LastReminderUtc ?? invitation.SentUtc;

        return last is not null && utcNow >= last.Value.AddDays(Math.Max(settings.ReminderEveryDays, 1));
    }

    /// <summary>
    /// Gets the start of the day a time falls in, in a time zone, as UTC: the day the daily e-mail limit counts.
    /// </summary>
    /// <param name="utcNow">The time, in UTC.</param>
    /// <param name="timeZoneId">The time zone, or empty for UTC.</param>
    /// <returns>The start of the day, in UTC.</returns>
    public static DateTime DayStartUtc(DateTime utcNow, string timeZoneId)
    {
        var local = CalendarTime.ToLocal(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timeZoneId);

        // A midnight a daylight-saving change skips starts the day when the clocks move forward.
        return CalendarTime.ToUtc(local.Date, timeZoneId);
    }

    /// <summary>
    /// Gets the invitation settings of a collector.
    /// </summary>
    /// <param name="collector">The collector.</param>
    /// <returns>The settings.</returns>
    public static SurveyInvitationSettings Settings(SurveyCollector collector)
        => collector is not null && collector.TryGet<SurveyInvitationSettings>(out var settings) && settings is not null ? settings : new SurveyInvitationSettings();

    /// <summary>
    /// Gets the context an invitation's responses belong to, or <see langword="null"/>.
    /// </summary>
    /// <param name="invitation">The invitation.</param>
    /// <returns>The context, or <see langword="null"/>.</returns>
    public static RecordRef? ContextOf(SurveyInvitation invitation)
        => invitation is not null && invitation.TryGet<SurveyInvitationContext>(out var stored) && RecordRef.TryParse(stored?.Context, out var context) ? context : null;

    /// <summary>
    /// Finds the survey's collector for personal links, creating it when it has none.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The collector.</returns>
    public async Task<SurveyCollector> EnsureCollectorAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        var collectors = await _collectorStore.ListBySurveyAsync(survey.ItemId, cancellationToken);
        var collector = collectors.FirstOrDefault(candidate => candidate.Kind == SurveyCollectorKinds.Invitation);

        if (collector is not null)
        {
            return collector;
        }

        collector = NewPersonalLinksCollector(survey.ItemId, _clock.UtcNow);

        await _collectorStore.CreateAsync(collector, cancellationToken);

        return collector;
    }

    /// <summary>
    /// Makes (without saving) a survey's collector for personal links, the invitation list a survey gets when it has none.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The collector.</returns>
    public static SurveyCollector NewPersonalLinksCollector(string surveyId, DateTime utcNow)
        => new()
        {
            ItemId = IdGenerator.GenerateId(),
            SurveyId = surveyId,
            Name = "Personal links",
            Kind = SurveyCollectorKinds.Invitation,
            Token = SurveyNames.NewToken(),
            Status = CollectorStatus.Open,
            CreatedUtc = utcNow,
        };

    /// <summary>
    /// Re-issues the stored token hash and seal lookup value of every invitation of a survey, after its anonymity secret
    /// was reset. The links sent before stop working; sending the invitations again sends the new links.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many invitations changed.</returns>
    public async Task<int> ReissueTokensAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        // A page at a time, so a survey with many invitations isn't loaded all at once.
        return await _invitationStore.ForEachBySurveyAsync(survey.ItemId, async invitation =>
        {
            var tokenHash = invitation.TokenHash;
            var sealLookup = invitation.SealLookup;

            await IssueTokenAsync(survey, invitation, cancellationToken);

            return !string.Equals(tokenHash, invitation.TokenHash, StringComparison.Ordinal) || !string.Equals(sealLookup, invitation.SealLookup, StringComparison.Ordinal);
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets whether a value is exactly one e-mail address: no list, no display name, no spaces.
    /// </summary>
    /// <param name="email">The value.</param>
    /// <returns><see langword="true"/> for a single address.</returns>
    public static bool IsSingleEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || email.AsSpan().IndexOfAny(_notInAddress) >= 0)
        {
            return false;
        }

        return MailAddress.TryCreate(email, out var parsed)
            && string.Equals(parsed.Address, email, StringComparison.Ordinal)
            && string.IsNullOrEmpty(parsed.DisplayName);
    }

    // Makes an invitation's token and keeps its hash and seal lookup value on the invitation. The caller saves it.
    private async Task<string> IssueTokenAsync(Survey survey, SurveyInvitation invitation, CancellationToken cancellationToken)
    {
        var issued = await IssueAsync(survey, invitation.ItemId, cancellationToken);

        issued.ApplyTo(invitation);

        return issued.Token;
    }

    // Makes an invitation's token (the same every time for the same secret), its hash and seal lookup value, without
    // changing the invitation.
    private async Task<IssuedToken> IssueAsync(Survey survey, string invitationId, CancellationToken cancellationToken)
    {
        var secret = await _secrets.RevealAsync(survey, cancellationToken);
        var token = SurveyInvitationTokens.Create(secret, invitationId);

        return new IssuedToken(token, SurveyInvitationTokens.Hash(token), SurveyRespondentKeys.InvitationSealLookup(secret, SurveyRespondentKeys.InvitationSeal(secret, invitationId)));
    }

    // A survey that has no anonymity secret yet makes and saves one the first time it is read: a bulk send reads it once
    // before its first commit, so the survey it saves is the copy this session tracks, never one loaded before a commit.
    private async Task EnsureSecretAsync(Survey survey)
    {
        if (string.IsNullOrEmpty(survey.AnonymitySecret))
        {
            await _secrets.RevealAsync(survey, CancellationToken.None);
        }
    }

    // Sends the first sends of a list (Invite many, Send): each is delivered in turn; once the scope's e-mails are held
    // (or the send was asked to stop), the e-mails left are held in a few reads rather than one by one. linkOf gives an
    // invitation's link when its caller already made it; otherwise the link is made, and the token re-issued, at the send.
    // Returns how many were sent, and the current copy of each invitation the send changed.
    private async Task<(int Sent, Dictionary<string, SurveyInvitation> Current)> DeliverFirstSendsAsync(Survey survey, SurveyCollector collector, IReadOnlyCollection<SurveyInvitation> invitations, Func<SurveyInvitation, string> linkOf)
    {
        var current = new Dictionary<string, SurveyInvitation>(StringComparer.Ordinal);
        var toHold = new List<SurveyInvitation>();
        var sent = 0;

        foreach (var invitation in invitations)
        {
            if (IsEmailInvitation(invitation) && (!CanEmail || _emailLimit.IsClosed || StopRequested))
            {
                toHold.Add(invitation);

                continue;
            }

            if (StopRequested)
            {
                continue;
            }

            var delivery = await DeliverAsync(survey, collector, invitation, linkOf?.Invoke(invitation), isReminder: false, CancellationToken.None);

            current[invitation.ItemId] = delivery.Invitation;

            if (delivery.Sent)
            {
                sent++;
            }
        }

        foreach (var chunk in toHold.Chunk(SurveysConstants.PageSize))
        {
            var loaded = await _invitationStore.LoadCurrentAsync(chunk, CancellationToken.None);

            foreach (var held in loaded)
            {
                if (Hold(held, isReminder: false))
                {
                    await _invitationStore.UpdateAsync(held, CancellationToken.None);
                }

                current[held.ItemId] = held;
            }

            await _invitationStore.FlushAsync(CancellationToken.None);

            // Written; let go of, so a large held batch isn't kept in the session (R8-Mi13).
            foreach (var held in loaded)
            {
                _invitationStore.Detach(held);
            }
        }

        return (sent, current);
    }

    // Sends one invitation or reminder. The link is built (and the token re-issued) only once the invitation is known to
    // still need the e-mail. A bulk send commits just before the e-mail goes out, so nothing of the send is locked while
    // the mail server works, and again right after its marks are written (R8-M3); every write re-reads the invitation
    // (SaveCheckedAsync), so a copy loaded before a commit is never saved.
    private async Task<DeliveryResult> DeliverAsync(Survey survey, SurveyCollector collector, SurveyInvitation invitation, string link, bool isReminder, CancellationToken cancellationToken)
    {
        var sender = _senders.LastOrDefault();

        if (survey is null || collector is null || sender is null)
        {
            return new DeliveryResult(false, invitation);
        }

        var isEmail = IsEmailInvitation(invitation);

        // Once the scope's e-mails are held (the daily limit's lock was busy, its sending window passed, or today's
        // limit is reached), an e-mail is held right away, without asking the opt-out checks; they are asked when it is
        // sent. So a batch that stopped sending has only holds left before its save.
        // Without an e-mail service (the Email feature is off) an e-mail is held the same way, and the daily limit's lock
        // isn't taken for it.
        if (isEmail && (_emailLimit.IsClosed || !sender.CanSendEmail))
        {
            return new DeliveryResult(false, await HoldAsync(invitation, isReminder, cancellationToken));
        }

        // Read again just before the send (R7-M7): the invitation was loaded with its page, maybe minutes ago, and since
        // then its respondent may have completed, opened or started it, or another send reminded it. A copy that no
        // longer matches the database is loaded again, and sent only when it still needs this e-mail.
        var row = await _invitationStore.FindRowAsync(invitation.ItemId, cancellationToken);

        if (row is null)
        {
            return new DeliveryResult(false, invitation);
        }

        if (!Matches(row, invitation))
        {
            var loaded = invitation;

            invitation = await _invitationStore.ReloadAsync(loaded.ItemId, cancellationToken);

            if (invitation is null || !StillNeedsSending(loaded, invitation, isReminder))
            {
                return new DeliveryResult(false, invitation ?? loaded);
            }
        }

        var optedOut = await HasOptedOutAsync(survey, invitation, cancellationToken);

        // A check that can't tell (the opt-out list can't be read) sends nothing and changes nothing but the hold mark,
        // so the invitation goes out once the list can be read again.
        if (optedOut is null)
        {
            return new DeliveryResult(false, await HoldAsync(invitation, isReminder, cancellationToken));
        }

        if (optedOut.Value)
        {
            invitation = await SaveCheckedAsync(invitation, current =>
            {
                if (current.Status == SurveyInvitationStatus.Completed)
                {
                    return false;
                }

                current.Status = SurveyInvitationStatus.OptedOut;
                current.Properties.Remove(nameof(SurveyInvitationHold));

                return true;
            }, cancellationToken);

            return new DeliveryResult(false, invitation);
        }

        // The site's daily e-mail limit, checked under the site's e-mail lock: an invitation over it, or one whose
        // sender can't get the lock in time, is not sent. A first send is held back and the hourly reminder task sends
        // it on a later run; a reminder simply stays due.
        if (isEmail && !await _emailLimit.CanSendAsync(cancellationToken))
        {
            return new DeliveryResult(false, await HoldAsync(invitation, isReminder, cancellationToken));
        }

        // The link of an invitation sent again is made now. When its stored token hash no longer matches (the secret was
        // reset, or the invitation predates its seal lookup value), the new hash is saved with the send's marks, on the
        // copy current then (R8-Mi9), never on this one.
        IssuedToken reissued = null;

        if (link is null)
        {
            var issued = await IssueAsync(survey, invitation.ItemId, cancellationToken);

            link = await _links.BuildAsync(issued.Token);
            reissued = issued.Differs(invitation) ? issued : null;
        }

        // A bulk send commits before the e-mail goes out (R8-M3), so it holds no lock while the mail server works, and
        // checks there, between commits, whether it was asked to stop.
        if (!await CommitBeforeSendAsync())
        {
            return new DeliveryResult(false, invitation);
        }

        var delivery = await sender.SendAsync(survey, collector, invitation, link, isReminder, cancellationToken);
        var now = _clock.UtcNow;

        // A send that may work later (the mail server failed or is down) keeps a first send pending and held, for the
        // hourly reminder task; one whose failures used up MaxSendAttempts is marked bounced, so it stops filling the
        // task's held reads. A reminder simply stays due, up to as many attempts.
        if (delivery == SurveyInvitationDelivery.RetryLater)
        {
            invitation = await RecordFailureAsync(invitation, isEmail, isReminder, now, reissued, cancellationToken);
        }
        else if (delivery != SurveyInvitationDelivery.Delivered)
        {
            invitation = await SaveCheckedAsync(invitation, current =>
            {
                reissued?.ApplyTo(current);
                current.Properties.Remove(nameof(SurveyInvitationHold));

                if (isReminder)
                {
                    // A reminder that can never be delivered stops the reminders; the invitation stays open.
                    current.Put(new SurveyInvitationReminderFailures { Stopped = true });
                }
                else if (current.Status == SurveyInvitationStatus.Pending)
                {
                    current.Status = SurveyInvitationStatus.Bounced;
                }

                return true;
            }, cancellationToken);
        }
        else
        {
            if (isEmail)
            {
                // The mail server takes e-mails: the failures of the run are the addresses' (counted when its e-mail work
                // ends), and the run goes on.
                _emailLimit.AddSent();
                _failuresInARow = 0;
                ConfirmFailures();
            }

            invitation = await SaveCheckedAsync(invitation, current =>
            {
                reissued?.ApplyTo(current);
                current.Properties.Remove(nameof(SurveyInvitationHold));

                if (isReminder)
                {
                    current.RemindersSent++;
                    current.LastReminderUtc = now;
                    current.Properties.Remove(nameof(SurveyInvitationReminderFailures));
                }
                else
                {
                    current.SentUtc = now;

                    if (current.Status == SurveyInvitationStatus.Pending)
                    {
                        current.Status = SurveyInvitationStatus.Sent;
                    }
                }

                return true;
            }, cancellationToken);
        }

        // The send's marks are committed right after they are written (R8-M3): a respondent who opens the e-mail at once
        // never waits on this send, and a later failure or a cancel of the send doesn't undo them.
        await CommitAfterSendAsync();

        return new DeliveryResult(delivery == SurveyInvitationDelivery.Delivered, invitation);
    }

    // Saves a change to an invitation: the change is applied to the copy the database has now, never to the copy the
    // send holds (loaded with its page, or before one of the send's commits), so a completion, an open or a start
    // committed meanwhile is kept and only the send's own fields change (R7-M7, R8-Mi9, R8-Mi10). It is written at once.
    // YesSql's own concurrency check would cancel the whole session, and with it the marks of every e-mail sent before.
    // A gap of milliseconds remains between that read and the write, in which another request's commit would be
    // overwritten (R8-Mi31): closing it needs a concurrency check on every writer of invitations. A bulk send lets go of
    // the copy once written (R8-Mi13), so a long walk keeps none of the copies it wrote.
    private Task<SurveyInvitation> SaveCheckedAsync(SurveyInvitation invitation, Func<SurveyInvitation, bool> apply, CancellationToken cancellationToken)
        => _invitationStore.UpdateCurrentAsync(invitation, apply, detach: _commitsAsItGoes, cancellationToken);

    // Whether the copy of an invitation a send holds is what the database has now.
    private static bool Matches(SurveyInvitationIndex row, SurveyInvitation invitation)
        => string.Equals(row.Status, invitation.Status.ToString(), StringComparison.Ordinal)
            && row.RemindersSent == invitation.RemindersSent
            && row.RemindersStopped == SurveyInvitationReminderFailures.HaveStopped(invitation)
            && SameTime(row.LastReminderUtc, invitation.LastReminderUtc)
            && SameTime(row.SentUtc, invitation.SentUtc);

    // Whether an invitation loaded again just before its send still needs the e-mail the walk was about to send: not
    // completed or closed meanwhile, and (for a reminder) not reminded by another send.
    private static bool StillNeedsSending(SurveyInvitation loaded, SurveyInvitation current, bool isReminder)
    {
        if (!isReminder)
        {
            return current.Status == SurveyInvitationStatus.Pending;
        }

        return _remindable.Contains(current.Status)
            && !SurveyInvitationReminderFailures.HaveStopped(current)
            && current.RemindersSent == loaded.RemindersSent
            && SameTime(current.LastReminderUtc, loaded.LastReminderUtc);
    }

    // Two stored times compare to the millisecond, the precision every supported database keeps.
    private static bool SameTime(DateTime? first, DateTime? second)
        => first is null ? second is null : second is not null && Math.Abs((first.Value - second.Value).Ticks) < TimeSpan.TicksPerMillisecond;

    // Runs under the e-mail lock, so with CancellationToken.None: a cancel stops it between commits instead.
    private async Task<int> SendHeldAsync(Survey survey, SurveyCollector collector, HashSet<string> sentIds, HashSet<string> tried, bool failedBefore)
    {
        // Held invitations are e-mails; while today's limit is still reached (or its lock is busy, or the mail server is
        // down) they all wait for another run.
        if (!CanEmail || _emailLimit.IsClosed || StopRequested)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var sent = 0;

        // Read by the indexed hold mark, so held invitations are found however many never-sent ones the collector has, a
        // page at a time for as long as the send may e-mail: a large held list goes out in one run when the limit and the
        // sending window allow, not one page an hour. One pass sends those the mail server never failed, the other those
        // it did; an invitation is tried once a run.
        await _invitationStore.VisitHeldByCollectorAsync(
            collector.ItemId,
            async invitation =>
            {
                var failed = invitation.TryGet<SurveyInvitationHold>(out var hold) && hold?.LastFailedUtc is not null;

                if (failed != failedBefore || !tried.Add(invitation.ItemId))
                {
                    return;
                }

                if (invitation.ExpiresUtc is { } expires && expires <= now)
                {
                    await SaveCheckedAsync(invitation, current =>
                    {
                        if (current.Status != SurveyInvitationStatus.Pending)
                        {
                            return false;
                        }

                        current.Status = SurveyInvitationStatus.Expired;
                        current.Properties.Remove(nameof(SurveyInvitationHold));

                        return true;
                    }, CancellationToken.None);

                    return;
                }

                if ((await DeliverAsync(survey, collector, invitation, link: null, isReminder: false, CancellationToken.None)).Sent)
                {
                    sentIds.Add(invitation.ItemId);
                    sent++;
                }
            },
            stop: () => _emailLimit.IsClosed || StopRequested,
            SurveysConstants.PageSize,
            CancellationToken.None);

        return sent;
    }

    // Whether the site can send e-mail invitations now (an e-mail service is turned on).
    private bool CanEmail
        => _senders.LastOrDefault() is { CanSendEmail: true };

    // Whether a request invites an e-mail address rather than a user.
    private static bool IsEmail(SurveyInvitationRequest request)
        => string.IsNullOrEmpty(request.UserId) && !string.IsNullOrWhiteSpace(request.Email);

    // Takes the tenant's invitation e-mail lock (see SurveyInvitationEmailLimit) before the invitations to e-mail are
    // read, so they are read after every other sender saved its sends: an invitation another send just sent or reminded
    // is read as sent or reminded, and isn't e-mailed again. Without an e-mail service nothing is e-mailed, and the lock
    // isn't taken. Returns whether the scope holds the lock.
    private async Task<bool> EnterEmailSendAsync(CancellationToken cancellationToken)
    {
        if (!CanEmail)
        {
            return false;
        }

        if (!_emailLimit.HoldsLock && !_emailLimit.IsClosed)
        {
            await _emailLimit.CanSendAsync(cancellationToken);
        }

        if (_emailLimit.LockUnavailable)
        {
            AnotherSendIsGoingOut = true;
        }

        return _emailLimit.HoldsLock;
    }

    // Takes a list's notification lock, which a send holds while it notifies the list's users, until it committed; or
    // null, when another send holds it past the wait.
    private async Task<ILocker> EnterNotificationsAsync(string collectorId)
    {
        var key = NotificationLockPrefix + collectorId;
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key, SurveyInvitationEmailLimit.LockTimeout, SurveyInvitationEmailLimit.LockExpiration);

        if (locked)
        {
            return locker;
        }

        AnotherSendIsGoingOut = true;
        _logger.LogWarning("The notification lock '{LockKey}' of an invitation list could not be acquired in time; another send of the list is notifying its users.", key);

        return null;
    }

    // Whether another send holds the tenant's invitation e-mail lock past its wait (asked when this scope e-mails at
    // all): this scope then holds its e-mails and leaves existing invitations unchanged (R8-Mi11).
    private async Task<bool> IsAnotherSendGoingOutAsync(CancellationToken cancellationToken)
        => !await EnterEmailSendAsync(cancellationToken) && _emailLimit.LockUnavailable;

    // Whether an invitation goes to an e-mail address rather than a user.
    private static bool IsEmailInvitation(SurveyInvitation invitation)
        => string.IsNullOrEmpty(invitation.UserId) && !string.IsNullOrWhiteSpace(invitation.Email);

    // Marks a bulk send (one that has its own scope: Invite many, Send, reminders) as committing as it goes, and asking
    // the cancellation token only between its commits (R8-M3).
    private void BeginBulkSend(CancellationToken cancellationToken)
    {
        _commitsAsItGoes = true;
        _stopToken = cancellationToken;
    }

    // Commits what the bulk send wrote before an e-mail goes out, so it holds no lock while the mail server works: on
    // SQLite even a read keeps the database's write lock until the commit. Returns false, sending nothing, once the send
    // was asked to stop: what it committed stays.
    private async Task<bool> CommitBeforeSendAsync()
    {
        if (!_commitsAsItGoes)
        {
            return true;
        }

        await _invitationStore.CommitAsync(CancellationToken.None);

        return !StopRequested;
    }

    // Commits a send's marks right after they are written.
    private async Task CommitAfterSendAsync()
    {
        if (_commitsAsItGoes)
        {
            await _invitationStore.CommitAsync(CancellationToken.None);
        }
    }

    // Ends a bulk send's e-mail work: the failures of the run are counted, everything is committed, and the e-mail lock
    // is let go of, so the next send reads what this one e-mailed.
    private async Task EndEmailWorkAsync()
    {
        await ResolveFailuresAsync(CancellationToken.None);
        await _invitationStore.CommitAsync(CancellationToken.None);
        await _emailLimit.ReleaseAsync();
    }

    // Holds back a first send for the hourly reminder task, on the current copy of the invitation (R8-Mi10): a copy of
    // the page, loaded minutes ago, may say Pending although the invitation was opened since.
    private async Task<SurveyInvitation> HoldAsync(SurveyInvitation invitation, bool isReminder, CancellationToken cancellationToken)
    {
        // Only a first send is marked: a reminder that was held back stays due and is tried again on the next run.
        if (isReminder || invitation.Status != SurveyInvitationStatus.Pending || (invitation.TryGet<SurveyInvitationHold>(out var held) && held is not null))
        {
            return invitation;
        }

        return await SaveCheckedAsync(invitation, current => Hold(current, isReminder), cancellationToken);
    }

    // Marks a pending first send held; false when there is nothing to mark.
    private bool Hold(SurveyInvitation invitation, bool isReminder)
    {
        if (isReminder || invitation.Status != SurveyInvitationStatus.Pending || (invitation.TryGet<SurveyInvitationHold>(out var held) && held is not null))
        {
            return false;
        }

        invitation.Put(new SurveyInvitationHold { HeldUtc = _clock.UtcNow });

        return true;
    }

    // Expires an open invitation whose link expired, on its current copy.
    private Task<SurveyInvitation> ExpireAsync(SurveyInvitation invitation, DateTime now)
        => SaveCheckedAsync(invitation, current =>
        {
            if (!_remindable.Contains(current.Status) || current.ExpiresUtc is not { } expires || expires > now)
            {
                return false;
            }

            current.Status = SurveyInvitationStatus.Expired;

            return true;
        }, CancellationToken.None);

    // Records a send the mail server failed, with the time of the failure: saving writes its index row again under a new
    // id, so the next run tries it after the others. Two e-mail failures in a row stop the scope's sending, as in an
    // outage, so it holds its later e-mails. Whether a failure uses up an attempt is decided when the scope's e-mail work
    // ends (ResolveFailuresAsync, R8-M2). A failed notification to a user is no mail outage, and counts at once.
    private async Task<SurveyInvitation> RecordFailureAsync(SurveyInvitation invitation, bool isEmail, bool isReminder, DateTime now, IssuedToken reissued, CancellationToken cancellationToken)
    {
        if (isEmail)
        {
            RefusedEmails++;

            var index = _unconfirmedFailures.FindIndex(failure => failure.ItemId == invitation.ItemId && failure.IsReminder == isReminder);

            if (index >= 0)
            {
                // Failed again: confirmed only by an e-mail that goes out after this failure.
                _unconfirmedFailures[index] = (invitation.ItemId, isReminder, false);
            }
            else
            {
                _unconfirmedFailures.Add((invitation.ItemId, isReminder, false));
            }

            if (++_failuresInARow >= 2)
            {
                _emailLimit.HoldAfterMailServerFailure();
            }
        }

        return await SaveCheckedAsync(invitation, current =>
        {
            reissued?.ApplyTo(current);

            var changed = MarkFailure(current, isReminder, now) || reissued is not null;

            return !isEmail ? CountAttempt(current, isReminder, lone: false, now) || changed : changed;
        }, cancellationToken);
    }

    // Marks every failure so far as its address's: an e-mail just went out after them, so the mail server was taking
    // e-mails (R9-M2).
    private void ConfirmFailures()
    {
        for (var index = 0; index < _unconfirmedFailures.Count; index++)
        {
            _unconfirmedFailures[index] = _unconfirmedFailures[index] with { Confirmed = true };
        }
    }

    // Ends a send's e-mail work for its failures: a failure an e-mail of the send went out after was its address's and
    // uses up an attempt (R8-M2), and so does one tried last in a send that e-mailed others and didn't stop. The
    // failures in a row that stopped a send (an outage or a throttle that started partway through), and those of a send
    // that e-mailed nobody, may be an outage: each uses one up at most once per SurveyInvitationHold.LoneFailureInterval
    // (R9-M2). So several refused addresses in one list bounce after their attempts, however they sit in the list, a
    // single refused address on a busy list bounces after a few hourly runs, and a throttle that hits a run partway
    // through never bounces valid addresses: at most one attempt a day, as in an outage.
    private async Task ResolveFailuresAsync(CancellationToken cancellationToken)
    {
        if (_unconfirmedFailures.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;
        var failures = _unconfirmedFailures.ToArray();

        _unconfirmedFailures.Clear();

        // The failures no e-mail went out after are the ones in a row that stopped the send, when it stopped.
        var stoppedAsInAnOutage = _emailLimit.MailServerFailed;
        var sentAny = _emailLimit.HasSent;

        foreach (var (itemId, isReminder, confirmed) in failures)
        {
            var lone = !confirmed && (stoppedAsInAnOutage || !sentAny);

            // Loaded again (a copy this session may track is let go of first), so the count lands on the current copy.
            var current = await _invitationStore.ReloadAsync(itemId, cancellationToken);

            if (current is null)
            {
                continue;
            }

            if (CountAttempt(current, isReminder, lone, now))
            {
                await _invitationStore.UpdateAsync(current, cancellationToken);
                await _invitationStore.FlushAsync(cancellationToken);
            }

            if (_commitsAsItGoes)
            {
                _invitationStore.Detach(current);
            }
        }
    }

    // Marks a failure on an invitation, without counting an attempt: a reminder's failures, or a first send's hold.
    private static bool MarkFailure(SurveyInvitation invitation, bool isReminder, DateTime now)
    {
        if (isReminder)
        {
            var failures = invitation.TryGet<SurveyInvitationReminderFailures>(out var stored) && stored is not null ? stored : new SurveyInvitationReminderFailures();

            failures.LastFailedUtc = now;
            invitation.Put(failures);

            return true;
        }

        if (invitation.Status != SurveyInvitationStatus.Pending)
        {
            return false;
        }

        var hold = invitation.TryGet<SurveyInvitationHold>(out var held) && held is not null ? held : new SurveyInvitationHold { HeldUtc = now };

        hold.LastFailedUtc = now;
        invitation.Put(hold);

        return true;
    }

    // Uses up one of an invitation's attempts. A reminder that used them all gets no more reminders, so one bad address
    // isn't retried every hour until it expires; a first send is marked bounced, and stops filling the held reads.
    private static bool CountAttempt(SurveyInvitation invitation, bool isReminder, bool lone, DateTime now)
    {
        if (isReminder)
        {
            if (!invitation.TryGet<SurveyInvitationReminderFailures>(out var failures) || failures is null || !MayCount(failures.LastCountedUtc, lone, now))
            {
                return false;
            }

            failures.FailedAttempts++;
            failures.LastCountedUtc = now;
            failures.Stopped = failures.FailedAttempts >= SurveyInvitationHold.MaxSendAttempts;
            invitation.Put(failures);

            return true;
        }

        if (invitation.Status != SurveyInvitationStatus.Pending
            || !invitation.TryGet<SurveyInvitationHold>(out var hold)
            || hold is null
            || !MayCount(hold.LastCountedUtc, lone, now))
        {
            return false;
        }

        hold.FailedAttempts++;
        hold.LastCountedUtc = now;

        if (hold.FailedAttempts >= SurveyInvitationHold.MaxSendAttempts)
        {
            invitation.Status = SurveyInvitationStatus.Bounced;
            invitation.Properties.Remove(nameof(SurveyInvitationHold));
        }
        else
        {
            invitation.Put(hold);
        }

        return true;
    }

    private static bool MayCount(DateTime? lastCountedUtc, bool lone, DateTime now)
        => !lone || lastCountedUtc is not { } last || now - last >= SurveyInvitationHold.LoneFailureInterval;

    private async Task<bool?> HasOptedOutAsync(Survey survey, SurveyInvitation invitation, CancellationToken cancellationToken)
    {
        if (invitation.Status == SurveyInvitationStatus.OptedOut)
        {
            return true;
        }

        // A user invitation goes to the user's notification inbox, which follows the user's own notification
        // preferences; only e-mail invitations are checked against opt-outs and unsubscribes.
        if (!string.IsNullOrEmpty(invitation.UserId) || string.IsNullOrWhiteSpace(invitation.Email))
        {
            return false;
        }

        var unknown = false;

        foreach (var policy in _recipientPolicies)
        {
            var decision = await policy.HasOptedOutAsync(survey, invitation, cancellationToken);

            if (decision == true)
            {
                return true;
            }

            unknown |= decision is null;
        }

        return unknown ? null : false;
    }

    private async Task<bool> IsAnsweredAnonymouslyAsync(Survey survey, SurveyInvitation invitation, CancellationToken cancellationToken)
    {
        if (survey.Access?.Anonymity != AnonymityMode.Anonymous)
        {
            return false;
        }

        var key = await _secrets.RespondentKeyAsync(survey, new SurveyRespondentIdentity(), invitation, cancellationToken);

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var rows = await _participationStore.ListAsync(survey.ItemId, key, DateOnly.MinValue, cancellationToken);

        return rows.Any(row => row.CompletedCount > 0);
    }

    private async Task<SurveyInvitation> FindForRecipientAsync(string collectorId, string userId, PartyRef? recipient, string email, CancellationToken cancellationToken)
    {
        // Read by the index, one kind of recipient at a time, so a bulk invite doesn't read the whole collector for each
        // recipient. The user is matched first, then the recipient, then (with neither) the e-mail address.
        if (!string.IsNullOrEmpty(userId))
        {
            var byUser = (await _invitationStore.ListForRecipientAsync(collectorId, userId, null, null, cancellationToken))
                .FirstOrDefault(invitation => string.Equals(invitation.UserId, userId, StringComparison.Ordinal));

            if (byUser is not null)
            {
                return byUser;
            }
        }

        if (recipient is not null)
        {
            var byRecipient = (await _invitationStore.ListForRecipientAsync(collectorId, null, recipient.Value.ToString(), null, cancellationToken))
                .FirstOrDefault(invitation => invitation.Recipient == recipient);

            if (byRecipient is not null)
            {
                return byRecipient;
            }
        }

        if (email is null || !string.IsNullOrEmpty(userId) || recipient is not null)
        {
            return null;
        }

        return (await _invitationStore.ListForRecipientAsync(collectorId, null, null, email, cancellationToken))
            .FirstOrDefault(invitation => string.Equals(invitation.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    // What a delivery did, and the invitation as it stands now: a copy loaded again when another request changed it.
    private readonly record struct DeliveryResult(bool Sent, SurveyInvitation Invitation);

    // An invitation's token, and the hash and seal lookup value stored for it.
    private sealed record IssuedToken(string Token, string Hash, string SealLookup)
    {
        // Whether the invitation stores other values (the secret was reset, or it predates its seal lookup value).
        public bool Differs(SurveyInvitation invitation)
            => !string.Equals(Hash, invitation.TokenHash, StringComparison.Ordinal) || !string.Equals(SealLookup, invitation.SealLookup, StringComparison.Ordinal);

        public void ApplyTo(SurveyInvitation invitation)
        {
            invitation.TokenHash = Hash;
            invitation.SealLookup = SealLookup;
        }
    }
}
