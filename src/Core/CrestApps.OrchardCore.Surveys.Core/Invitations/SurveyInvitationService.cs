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
using OrchardCore.Settings;

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
    private readonly ISiteService _siteService;

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
    /// <param name="siteService">The site service, for the daily e-mail limit; without it the default limit applies.</param>
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
        ISiteService siteService = null)
    {
        _siteService = siteService;
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
    public async Task<SurveyInvitationResult> InviteAsync(SurveyInvitationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await CreateAsync(request, requireRecipient: true, cancellationToken);

        if (result.Succeeded && result.Invitation.Status == SurveyInvitationStatus.Pending)
        {
            var survey = await _surveyStore.FindByIdAsync(result.Invitation.SurveyId, cancellationToken);
            var collector = await _collectorStore.FindByIdAsync(result.Invitation.CollectorId, cancellationToken);

            await DeliverAsync(survey, collector, result.Invitation, result.Link, isReminder: false, cancellationToken);
        }

        return result;
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

        var survey = await _surveyStore.FindByIdAsync(request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return new SurveyInvitationResult { Error = SurveyErrorCode.NotFound };
        }

        var collector = string.IsNullOrEmpty(request.CollectorId)
            ? await EnsureCollectorAsync(survey, cancellationToken)
            : await _collectorStore.FindByIdAsync(request.CollectorId, cancellationToken);

        if (collector is null || collector.SurveyId != survey.ItemId || collector.Kind != SurveyCollectorKinds.Invitation)
        {
            return new SurveyInvitationResult { Error = SurveyErrorCode.NotFound };
        }

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
        var collector = await _collectorStore.FindByIdAsync(collectorId, cancellationToken);
        var survey = collector is null ? null : await _surveyStore.FindByIdAsync(collector.SurveyId, cancellationToken);

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
    /// held back (see <see cref="SurveyInvitationHold"/>) are sent first.
    /// </summary>
    /// <param name="collectorId">The invitation collector.</param>
    /// <param name="force">Whether to remind every open invitation now, ignoring the interval (a manual resend).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many reminders, and invitations held back earlier, were sent.</returns>
    public async Task<int> RemindAsync(string collectorId, bool force, CancellationToken cancellationToken = default)
    {
        var collector = await _collectorStore.FindByIdAsync(collectorId, cancellationToken);
        var survey = collector is null ? null : await _surveyStore.FindByIdAsync(collector.SurveyId, cancellationToken);

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

        foreach (var invitation in await _invitationStore.ListByCollectorAsync(collector.ItemId, _remindable, SurveysConstants.PageSize, cancellationToken))
        {
            if (justSent.Contains(invitation.ItemId) || (!force && !IsReminderDue(invitation, settings, now)))
            {
                continue;
            }

            if (invitation.ExpiresUtc is { } expires && expires <= now)
            {
                invitation.Status = SurveyInvitationStatus.Expired;
                await _invitationStore.UpdateAsync(invitation, cancellationToken);

                continue;
            }

            // An anonymous response never names its invitation, so its completion is read from the participation
            // count of the invitation's pseudonymous key instead; such an invitation gets no more reminders.
            if (await IsAnsweredAnonymouslyAsync(survey, invitation, cancellationToken))
            {
                invitation.Status = SurveyInvitationStatus.Completed;
                await _invitationStore.UpdateAsync(invitation, cancellationToken);

                continue;
            }

            var link = await _links.BuildAsync(await ReissueTokenAsync(survey, invitation, cancellationToken));

            if (await DeliverAsync(survey, collector, invitation, link, isReminder: true, cancellationToken))
            {
                sent++;
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

        if (!_remindable.Contains(invitation.Status) || invitation.RemindersSent >= settings.ReminderCount)
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

        collector = new SurveyCollector
        {
            ItemId = IdGenerator.GenerateId(),
            SurveyId = survey.ItemId,
            Name = "Personal links",
            Kind = SurveyCollectorKinds.Invitation,
            Token = SurveyNames.NewToken(),
            Status = CollectorStatus.Open,
            CreatedUtc = _clock.UtcNow,
        };

        await _collectorStore.CreateAsync(collector, cancellationToken);

        return collector;
    }

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

        var isEmail = string.IsNullOrEmpty(invitation.UserId) && !string.IsNullOrWhiteSpace(invitation.Email);

        // The site's daily e-mail limit: an invitation over it is not sent. A first send is held back and the hourly
        // reminder task sends it on a later day; a reminder simply stays due.
        if (isEmail && await IsDailyEmailLimitReachedAsync(cancellationToken))
        {
            await HoldAsync(invitation, isReminder, cancellationToken);

            return false;
        }

        var delivered = await sender.SendAsync(survey, collector, invitation, link, isReminder, cancellationToken);
        var now = _clock.UtcNow;

        invitation.Properties.Remove(nameof(SurveyInvitationHold));

        if (!delivered)
        {
            if (!isReminder)
            {
                invitation.Status = SurveyInvitationStatus.Bounced;
                await _invitationStore.UpdateAsync(invitation, cancellationToken);
            }

            return false;
        }

        if (isReminder)
        {
            invitation.RemindersSent++;
            invitation.LastReminderUtc = now;
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
        var now = _clock.UtcNow;
        var sent = 0;
        var held = (await _invitationStore.ListByCollectorAsync(collector.ItemId, [SurveyInvitationStatus.Pending], SurveysConstants.PageSize, cancellationToken))
            .Where(invitation => invitation.TryGet<SurveyInvitationHold>(out var hold) && hold is not null)
            .ToList();

        // Held invitations are e-mails; while today's limit is still reached they all wait for another run.
        if (held.Count == 0 || await IsDailyEmailLimitReachedAsync(cancellationToken))
        {
            return 0;
        }

        foreach (var invitation in held)
        {
            if (invitation.ExpiresUtc is { } expires && expires <= now)
            {
                invitation.Status = SurveyInvitationStatus.Expired;
                invitation.Properties.Remove(nameof(SurveyInvitationHold));
                await _invitationStore.UpdateAsync(invitation, cancellationToken);

                continue;
            }

            var link = await _links.BuildAsync(await ReissueTokenAsync(survey, invitation, cancellationToken));

            if (await DeliverAsync(survey, collector, invitation, link, isReminder: false, cancellationToken))
            {
                sentIds.Add(invitation.ItemId);
                sent++;
            }
        }

        return sent;
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

    private async Task<bool> IsDailyEmailLimitReachedAsync(CancellationToken cancellationToken)
    {
        string timeZoneId = null;
        SurveySettings settings = null;

        if (_siteService is not null)
        {
            settings = await _siteService.GetSettingsAsync<SurveySettings>();
            timeZoneId = (await _siteService.GetSiteSettingsAsync())?.TimeZoneId;
        }

        if (string.IsNullOrEmpty(timeZoneId))
        {
            timeZoneId = _clock.GetSystemTimeZone()?.TimeZoneId ?? string.Empty;
        }

        var limit = Math.Max(1, settings?.InvitationEmailsPerDay ?? SurveySettings.DefaultInvitationEmailsPerDay);

        // Counted afresh for every e-mail, not once per scope, so parallel sends see each other's committed e-mails and
        // this scope's own sends (YesSql flushes them before the count). Two nodes sending at the very same moment can
        // still both pass the last free place, so the limit can be passed by a few e-mails, never by a whole batch.
        var emailed = await _invitationStore.CountEmailedSinceAsync(DayStartUtc(_clock.UtcNow, timeZoneId), cancellationToken);

        return emailed >= limit;
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
        var invitations = await _invitationStore.ListByCollectorAsync(collectorId, null, int.MaxValue, cancellationToken);

        return invitations.FirstOrDefault(invitation =>
            (!string.IsNullOrEmpty(userId) && string.Equals(invitation.UserId, userId, StringComparison.Ordinal))
            || (recipient is not null && invitation.Recipient == recipient)
            || (email is not null && string.IsNullOrEmpty(userId) && recipient is null && string.Equals(invitation.Email, email, StringComparison.OrdinalIgnoreCase)));
    }
}
