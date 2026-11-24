using System.Buffers;
using System.Net.Mail;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Creates personal invitations and their links, sends them and their reminders.
/// </summary>
public sealed class SurveyInvitationService : ISurveyInvitationService
{
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
    /// <param name="emailLimit">The site's daily e-mail limit, held under a lock per site day.</param>
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
        SurveyInvitationEmailLimit emailLimit)
    {
        _emailLimit = emailLimit;
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

    /// <inheritdoc />
    /// <remarks>
    /// Every send of a scope is one batch of the daily e-mail limit (see <see cref="SurveyInvitationEmailLimit"/>), so
    /// calling this once per recipient in one scope (a workflow loop) reads the limit and asks for its lock once. To
    /// invite many recipients at once, use <see cref="InviteManyAsync"/>.
    /// </remarks>
    public async Task<SurveyInvitationResult> InviteAsync(SurveyInvitationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsEmail(request))
        {
            await EnterEmailSendAsync(cancellationToken);
        }

        var result = await CreateAsync(request, requireRecipient: true, cancellationToken);

        if (result.Succeeded && result.Invitation.Status == SurveyInvitationStatus.Pending)
        {
            var survey = await FindLiveSurveyAsync(result.Invitation.SurveyId, cancellationToken);
            var collector = await _collectorStore.FindByIdAsync(result.Invitation.CollectorId, cancellationToken);

            await DeliverAsync(survey, collector, result.Invitation, result.Link, isReminder: false, cancellationToken);
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

        if (send && requests.Any(request => request is not null && IsEmail(request)))
        {
            await EnterEmailSendAsync(cancellationToken);
        }

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
                : await CreateCoreAsync(survey, collector, request, requireRecipient: true, cancellationToken));
        }

        if (!send || collector is null)
        {
            return results;
        }

        // Sent after every invitation exists, so once the batch stops sending only holds are left before the save, and
        // the daily limit's lock is held for the sends and little else.
        var delivered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var result in results)
        {
            if (result.Succeeded && result.Invitation.Status == SurveyInvitationStatus.Pending && delivered.Add(result.Invitation.ItemId))
            {
                await DeliverAsync(survey, collector, result.Invitation, result.Link, isReminder: false, cancellationToken);
            }
        }

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

        return await CreateCoreAsync(survey, collector, request, requireRecipient, cancellationToken);
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

    private async Task<SurveyInvitationResult> CreateCoreAsync(Survey survey, SurveyCollector collector, SurveyInvitationRequest request, bool requireRecipient, CancellationToken cancellationToken)
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

        var now = _clock.UtcNow;
        var settings = Settings(collector);
        var invitation = existing ?? new SurveyInvitation
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

        invitation.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? invitation.DisplayName ?? email : request.DisplayName.Trim();
        invitation.ExpiresUtc = request.ExpiresUtc ?? (settings.ExpiresAfterDays is > 0 ? now.AddDays(settings.ExpiresAfterDays.Value) : invitation.ExpiresUtc);

        if (invitation.Status == SurveyInvitationStatus.Expired && (invitation.ExpiresUtc is null || invitation.ExpiresUtc > now))
        {
            invitation.Status = SurveyInvitationStatus.Pending;
        }

        // Inviting again retries an invitation that bounced or whose address had opted out: it is pending again, and the
        // sender and the opt-out checks decide again when it is sent.
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

        var token = await IssueTokenAsync(survey, invitation, cancellationToken);

        if (existing is null)
        {
            await _invitationStore.CreateAsync(invitation, cancellationToken);
        }
        else
        {
            await _invitationStore.UpdateAsync(invitation, cancellationToken);
        }

        return new SurveyInvitationResult { Invitation = invitation, Link = await _links.BuildAsync(token) };
    }

    /// <summary>
    /// Sends the invitations of a collector that were never sent.
    /// </summary>
    /// <param name="collectorId">The invitation collector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were sent.</returns>
    public async Task<int> SendPendingAsync(string collectorId, CancellationToken cancellationToken = default)
    {
        await EnterEmailSendAsync(cancellationToken);

        var collector = await _collectorStore.FindByIdAsync(collectorId, cancellationToken);
        var survey = collector is null ? null : await FindLiveSurveyAsync(collector.SurveyId, cancellationToken);

        if (survey is null)
        {
            return 0;
        }

        var sent = 0;

        foreach (var invitation in await _invitationStore.ListByCollectorAsync(collector.ItemId, [SurveyInvitationStatus.Pending], SurveysConstants.PageSize, cancellationToken))
        {
            var link = await _links.BuildAsync(await ReissueTokenAsync(survey, invitation, cancellationToken));

            if (await DeliverAsync(survey, collector, invitation, link, isReminder: false, cancellationToken))
            {
                sent++;
            }
        }

        return sent;
    }

    /// <summary>
    /// Sends the reminders that are due for a collector: invitations sent, opened or started but not completed, that
    /// have had fewer reminders than the collector allows and none for the collector's interval. First sends that were
    /// held back (see <see cref="SurveyInvitationHold"/>) are sent first, for as long as the send may e-mail.
    /// </summary>
    /// <param name="collectorId">The invitation collector.</param>
    /// <param name="force">Whether to remind every open invitation now, ignoring the interval (a manual resend). An
    /// invitation already reminded today, in the site's time zone, is left out, so a resend that runs twice (a double
    /// click, or two people at once) reminds each person once.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many reminders, and invitations held back earlier, were sent.</returns>
    public async Task<int> RemindAsync(string collectorId, bool force, CancellationToken cancellationToken = default)
    {
        // The day's e-mail lock comes before any invitation is read, so another send of the list that is still going out
        // has saved what it sent before this one reads it.
        await EnterEmailSendAsync(cancellationToken);

        var collector = await _collectorStore.FindByIdAsync(collectorId, cancellationToken);
        var survey = collector is null ? null : await FindLiveSurveyAsync(collector.SurveyId, cancellationToken);

        if (survey is null || collector.Status != CollectorStatus.Open || survey.Status != SurveyStatus.Open)
        {
            return 0;
        }

        // First sends held back by the daily e-mail limit (or an unreadable opt-out list) go out now, before reminders.
        var justSent = new HashSet<string>(StringComparer.Ordinal);
        var sent = await SendHeldAsync(survey, collector, justSent, cancellationToken);
        var settings = Settings(collector);

        if (!force && settings.ReminderCount <= 0)
        {
            return sent;
        }

        var now = _clock.UtcNow;
        DateTime? remindedToday = force ? await _emailLimit.TodayStartUtcAsync() : null;

        // Expired invitations leave the open ones first, even when no reminder is due.
        await _invitationStore.VisitExpiredAsync(collector.ItemId, _remindable, now, async invitation =>
        {
            invitation.Status = SurveyInvitationStatus.Expired;
            await _invitationStore.UpdateAsync(invitation, cancellationToken);
        }, SurveysConstants.PageSize, cancellationToken);

        async Task RemindOneAsync(SurveyInvitation invitation)
        {
            if (justSent.Contains(invitation.ItemId))
            {
                return;
            }

            if (invitation.ExpiresUtc is { } expires && expires <= now)
            {
                invitation.Status = SurveyInvitationStatus.Expired;
                await _invitationStore.UpdateAsync(invitation, cancellationToken);

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
            if (await IsAnsweredAnonymouslyAsync(survey, invitation, cancellationToken))
            {
                invitation.Status = SurveyInvitationStatus.Completed;
                await _invitationStore.UpdateAsync(invitation, cancellationToken);

                return;
            }

            var link = await _links.BuildAsync(await ReissueTokenAsync(survey, invitation, cancellationToken));

            if (await DeliverAsync(survey, collector, invitation, link, isReminder: true, cancellationToken))
            {
                sent++;
            }
        }

        // E-mail invitations first, while the send may e-mail: that walk ends as soon as no more e-mail of this send goes
        // out (today's limit, a busy lock, the sending window or a failing mail server), so nothing is read, re-issued or
        // looked up for reminders that would only wait for the next run. Then the invitations of users, whose
        // notifications the limit doesn't count.
        var canEmail = CanEmail;
        bool EmailsStopped() => !canEmail || _emailLimit.IsClosed;

        foreach (var emails in new[] { true, false })
        {
            var stop = emails ? EmailsStopped : (Func<bool>)null;

            if (force)
            {
                // A manual resend reminds every open invitation of the list, a page at a time.
                await _invitationStore.VisitByCollectorAsync(collector.ItemId, _remindable, RemindOneAsync, emails, stop, SurveysConstants.PageSize, cancellationToken);
            }
            else
            {
                // Only the invitations the index says are due are read, so a large list with nothing due costs one query.
                var lastContactBefore = now.AddDays(-Math.Max(settings.ReminderEveryDays, 1));

                await _invitationStore.VisitDueForReminderAsync(collector.ItemId, _remindable, lastContactBefore, settings.ReminderCount, RemindOneAsync, emails, stop, SurveysConstants.PageSize, cancellationToken);
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
        var secret = await _secrets.RevealAsync(survey, cancellationToken);
        var token = SurveyInvitationTokens.Create(secret, invitation.ItemId);

        invitation.TokenHash = SurveyInvitationTokens.Hash(token);
        invitation.SealLookup = SurveyRespondentKeys.InvitationSealLookup(secret, SurveyRespondentKeys.InvitationSeal(secret, invitation.ItemId));

        return token;
    }

    // Makes the token of an invitation being sent again, saving the invitation when its stored hash no longer matches
    // (the secret was reset, or the invitation predates its seal lookup value), so the link it sends opens it.
    private async Task<string> ReissueTokenAsync(Survey survey, SurveyInvitation invitation, CancellationToken cancellationToken)
    {
        var tokenHash = invitation.TokenHash;
        var sealLookup = invitation.SealLookup;
        var token = await IssueTokenAsync(survey, invitation, cancellationToken);

        if (!string.Equals(tokenHash, invitation.TokenHash, StringComparison.Ordinal) || !string.Equals(sealLookup, invitation.SealLookup, StringComparison.Ordinal))
        {
            await _invitationStore.UpdateAsync(invitation, cancellationToken);
        }

        return token;
    }

    private async Task<bool> DeliverAsync(Survey survey, SurveyCollector collector, SurveyInvitation invitation, string link, bool isReminder, CancellationToken cancellationToken)
    {
        var sender = _senders.LastOrDefault();

        if (survey is null || collector is null || sender is null)
        {
            return false;
        }

        var isEmail = string.IsNullOrEmpty(invitation.UserId) && !string.IsNullOrWhiteSpace(invitation.Email);

        // Once the scope's e-mails are held (the daily limit's lock was busy, its sending window passed, or today's
        // limit is reached), an e-mail is held right away, without asking the opt-out checks; they are asked when it is
        // sent. So a batch that stopped sending has only holds left before its save.
        // Without an e-mail service (the Email feature is off) an e-mail is held the same way, and the daily limit's lock
        // isn't taken for it.
        if (isEmail && (_emailLimit.IsClosed || !sender.CanSendEmail))
        {
            await HoldAsync(invitation, isReminder, cancellationToken);

            return false;
        }

        var optedOut = await HasOptedOutAsync(survey, invitation, cancellationToken);

        // A check that can't tell (the opt-out list can't be read) sends nothing and changes nothing but the hold mark,
        // so the invitation goes out once the list can be read again.
        if (optedOut is null)
        {
            await HoldAsync(invitation, isReminder, cancellationToken);

            return false;
        }

        if (optedOut.Value)
        {
            invitation.Status = SurveyInvitationStatus.OptedOut;
            invitation.Properties.Remove(nameof(SurveyInvitationHold));
            await _invitationStore.UpdateAsync(invitation, cancellationToken);

            return false;
        }

        // The site's daily e-mail limit, checked under the lock of the site's day: an invitation over it, or one whose
        // sender can't get the lock in time, is not sent. A first send is held back and the hourly reminder task sends
        // it on a later run; a reminder simply stays due.
        if (isEmail && !await _emailLimit.CanSendAsync(cancellationToken))
        {
            await HoldAsync(invitation, isReminder, cancellationToken);

            return false;
        }

        var delivery = await sender.SendAsync(survey, collector, invitation, link, isReminder, cancellationToken);
        var now = _clock.UtcNow;

        // A send that may work later (the mail server failed or is down) keeps a first send pending and held, for the
        // hourly reminder task; one that failed MaxSendAttempts times is marked bounced, so it stops filling the task's
        // held reads. A reminder simply stays due, up to as many tries.
        if (delivery == SurveyInvitationDelivery.RetryLater)
        {
            // After an e-mail fails, the send's later e-mails are held without being tried, so a mail outage costs one
            // try a run rather than one per invitation. The try counts toward the attempts only when the mail server took
            // an earlier e-mail of this send, so the failure is this address's; when nothing of the send got through, the
            // server may be down, and no invitation uses up its attempts (or bounces) on an outage.
            var countAttempt = !isEmail || _emailLimit.HasSent;

            if (isEmail)
            {
                _emailLimit.HoldAfterMailServerFailure();
            }

            await FailAsync(invitation, isReminder, countAttempt, cancellationToken);

            return false;
        }

        invitation.Properties.Remove(nameof(SurveyInvitationHold));

        if (delivery != SurveyInvitationDelivery.Delivered)
        {
            if (isReminder)
            {
                // A reminder that can never be delivered stops the reminders; the invitation stays open.
                invitation.Put(new SurveyInvitationReminderFailures { Stopped = true });
            }
            else
            {
                invitation.Status = SurveyInvitationStatus.Bounced;
            }

            await _invitationStore.UpdateAsync(invitation, cancellationToken);

            return false;
        }

        if (isEmail)
        {
            _emailLimit.AddSent();
        }

        if (isReminder)
        {
            invitation.RemindersSent++;
            invitation.LastReminderUtc = now;
            invitation.Properties.Remove(nameof(SurveyInvitationReminderFailures));
        }
        else
        {
            invitation.SentUtc = now;

            if (invitation.Status == SurveyInvitationStatus.Pending)
            {
                invitation.Status = SurveyInvitationStatus.Sent;
            }
        }

        await _invitationStore.UpdateAsync(invitation, cancellationToken);

        return true;
    }

    private async Task<int> SendHeldAsync(Survey survey, SurveyCollector collector, HashSet<string> sentIds, CancellationToken cancellationToken)
    {
        // Held invitations are e-mails; while today's limit is still reached (or its lock is busy, or the mail server
        // failed) they all wait for another run.
        if (!CanEmail || _emailLimit.IsClosed)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var sent = 0;

        // Read by the indexed hold mark, so held invitations are found however many never-sent ones the collector has, a
        // page at a time for as long as the send may e-mail: a large held list goes out in one run when the limit and the
        // sending window allow, not one page an hour.
        await _invitationStore.VisitHeldByCollectorAsync(
            collector.ItemId,
            async invitation =>
            {
                if (invitation.ExpiresUtc is { } expires && expires <= now)
                {
                    invitation.Status = SurveyInvitationStatus.Expired;
                    invitation.Properties.Remove(nameof(SurveyInvitationHold));
                    await _invitationStore.UpdateAsync(invitation, cancellationToken);

                    return;
                }

                var link = await _links.BuildAsync(await ReissueTokenAsync(survey, invitation, cancellationToken));

                if (await DeliverAsync(survey, collector, invitation, link, isReminder: false, cancellationToken))
                {
                    sentIds.Add(invitation.ItemId);
                    sent++;
                }
            },
            stop: () => _emailLimit.IsClosed,
            SurveysConstants.PageSize,
            cancellationToken);

        return sent;
    }

    // Whether the site can send e-mail invitations now (an e-mail service is turned on).
    private bool CanEmail
        => _senders.LastOrDefault() is { CanSendEmail: true };

    // Whether a request invites an e-mail address rather than a user.
    private static bool IsEmail(SurveyInvitationRequest request)
        => string.IsNullOrEmpty(request.UserId) && !string.IsNullOrWhiteSpace(request.Email);

    // Takes the site's daily e-mail lock (see SurveyInvitationEmailLimit) before the invitations to e-mail are read, so
    // they are read after every other sender of the day saved its sends: an invitation another send just sent or reminded
    // is read as sent or reminded, and isn't e-mailed again. Without an e-mail service nothing is e-mailed, and the lock
    // isn't taken.
    private async Task EnterEmailSendAsync(CancellationToken cancellationToken)
    {
        if (CanEmail && !_emailLimit.IsClosed)
        {
            await _emailLimit.CanSendAsync(cancellationToken);
        }
    }

    private async Task HoldAsync(SurveyInvitation invitation, bool isReminder, CancellationToken cancellationToken)
    {
        // Only a first send is marked: a reminder that was held back stays due and is tried again on the next run.
        if (isReminder || invitation.Status != SurveyInvitationStatus.Pending || (invitation.TryGet<SurveyInvitationHold>(out var held) && held is not null))
        {
            return;
        }

        invitation.Put(new SurveyInvitationHold { HeldUtc = _clock.UtcNow });
        await _invitationStore.UpdateAsync(invitation, cancellationToken);
    }

    // Records a send the mail server failed. The invitation is saved either way, with the time of the failure: saving
    // writes its index row again under a new id, so the next run tries it after the others and an address that keeps
    // failing never stands first in line. Only a counted attempt uses up the invitation's tries.
    private async Task FailAsync(SurveyInvitation invitation, bool isReminder, bool countAttempt, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        if (isReminder)
        {
            // A reminder the mail server refused stays due for the next run, up to MaxSendAttempts tries; then the
            // invitation gets no more reminders, so one bad address isn't retried every hour until it expires.
            var failures = invitation.TryGet<SurveyInvitationReminderFailures>(out var stored) && stored is not null ? stored : new SurveyInvitationReminderFailures();

            failures.LastFailedUtc = now;

            if (countAttempt)
            {
                failures.FailedAttempts++;
                failures.Stopped = failures.FailedAttempts >= SurveyInvitationHold.MaxSendAttempts;
            }

            invitation.Put(failures);
            await _invitationStore.UpdateAsync(invitation, cancellationToken);

            return;
        }

        if (invitation.Status != SurveyInvitationStatus.Pending)
        {
            return;
        }

        var hold = invitation.TryGet<SurveyInvitationHold>(out var held) && held is not null ? held : new SurveyInvitationHold { HeldUtc = now };

        hold.LastFailedUtc = now;

        if (countAttempt)
        {
            hold.FailedAttempts++;
        }

        if (hold.FailedAttempts >= SurveyInvitationHold.MaxSendAttempts)
        {
            invitation.Status = SurveyInvitationStatus.Bounced;
            invitation.Properties.Remove(nameof(SurveyInvitationHold));
        }
        else
        {
            invitation.Put(hold);
        }

        await _invitationStore.UpdateAsync(invitation, cancellationToken);
    }

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
}
