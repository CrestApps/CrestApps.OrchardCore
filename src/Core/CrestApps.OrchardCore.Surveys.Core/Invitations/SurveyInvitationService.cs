using CrestApps.Core;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
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

    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyInvitationStore _invitationStore;
    private readonly SurveyInvitationLinks _links;
    private readonly IEnumerable<ISurveyInvitationSender> _senders;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationService"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="invitationStore">The invitation store.</param>
    /// <param name="links">The link builder.</param>
    /// <param name="senders">The invitation sender, when the module registers one.</param>
    /// <param name="clock">The clock.</param>
    public SurveyInvitationService(
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        SurveyInvitationStore invitationStore,
        SurveyInvitationLinks links,
        IEnumerable<ISurveyInvitationSender> senders,
        IClock clock)
    {
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _invitationStore = invitationStore;
        _links = links;
        _senders = senders;
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

        var token = SurveyInvitationTokens.Create(survey, invitation.ItemId);
        invitation.TokenHash = SurveyInvitationTokens.Hash(token);

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
            var link = await _links.BuildAsync(SurveyInvitationTokens.Create(survey, invitation.ItemId));

            if (await DeliverAsync(survey, collector, invitation, link, isReminder: false, cancellationToken))
            {
                sent++;
            }
        }

        return sent;
    }

    /// <summary>
    /// Sends the reminders that are due for a collector: invitations sent, opened or started but not completed, that
    /// have had fewer reminders than the collector allows and none for the collector's interval.
    /// </summary>
    /// <param name="collectorId">The invitation collector.</param>
    /// <param name="force">Whether to remind every open invitation now, ignoring the interval (a manual resend).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many reminders were sent.</returns>
    public async Task<int> RemindAsync(string collectorId, bool force, CancellationToken cancellationToken = default)
    {
        var collector = await _collectorStore.FindByIdAsync(collectorId, cancellationToken);
        var survey = collector is null ? null : await _surveyStore.FindByIdAsync(collector.SurveyId, cancellationToken);

        if (survey is null || collector.Status != CollectorStatus.Open || survey.Status != SurveyStatus.Open)
        {
            return 0;
        }

        var settings = Settings(collector);

        if (!force && settings.ReminderCount <= 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var sent = 0;

        foreach (var invitation in await _invitationStore.ListByCollectorAsync(collector.ItemId, _remindable, SurveysConstants.PageSize, cancellationToken))
        {
            if (!force && !IsReminderDue(invitation, settings, now))
            {
                continue;
            }

            if (invitation.ExpiresUtc is { } expires && expires <= now)
            {
                invitation.Status = SurveyInvitationStatus.Expired;
                await _invitationStore.UpdateAsync(invitation, cancellationToken);

                continue;
            }

            var link = await _links.BuildAsync(SurveyInvitationTokens.Create(survey, invitation.ItemId));

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

    private async Task<bool> DeliverAsync(Survey survey, SurveyCollector collector, SurveyInvitation invitation, string link, bool isReminder, CancellationToken cancellationToken)
    {
        var sender = _senders.LastOrDefault();

        if (survey is null || collector is null || sender is null)
        {
            return false;
        }

        var delivered = await sender.SendAsync(survey, collector, invitation, link, isReminder, cancellationToken);
        var now = _clock.UtcNow;

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

    private async Task<SurveyInvitation> FindForRecipientAsync(string collectorId, string userId, PartyRef? recipient, string email, CancellationToken cancellationToken)
    {
        var invitations = await _invitationStore.ListByCollectorAsync(collectorId, null, int.MaxValue, cancellationToken);

        return invitations.FirstOrDefault(invitation =>
            (!string.IsNullOrEmpty(userId) && string.Equals(invitation.UserId, userId, StringComparison.Ordinal))
            || (recipient is not null && invitation.Recipient == recipient)
            || (email is not null && string.IsNullOrEmpty(userId) && recipient is null && string.Equals(invitation.Email, email, StringComparison.OrdinalIgnoreCase)));
    }
}
