using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// Decides whether each email may leave its address now, later or not at all, and keeps the address's sending state:
/// limits and warm-up, pauses when servers push back, receiving domains that asked it to wait, and sending health.
/// </summary>
public interface IEmailSendingGovernor
{
    /// <summary>
    /// Decides whether one email may leave <paramref name="address"/>. A person's reply is only checked against the
    /// suppression list; an automated reply also waits for the address's pauses and for room under its hourly and daily
    /// limits; bulk mail is held to every limit and pause.
    /// </summary>
    /// <param name="address">The sending address.</param>
    /// <param name="settings">The address's email settings.</param>
    /// <param name="recipient">The recipient, normalized, or <see langword="null"/> when asking about the address alone.</param>
    /// <param name="kind">What the email is.</param>
    /// <param name="reserveTurn">Whether a held-back caller will wait for the time returned, so the next one gets a later turn. Only bulk mail takes turns.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The decision.</returns>
    Task<EmailSendDecision> EvaluateAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, string recipient, EmailSendKind kind, bool reserveTurn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an email the sending server accepted. Bulk mail getting through also ends a run of throttles or blocks.
    /// </summary>
    /// <param name="address">The sending address.</param>
    /// <param name="recipient">The recipient, normalized.</param>
    /// <param name="messageId">The email's Message-ID or provider identifier.</param>
    /// <param name="isBulk">Whether it was bulk mail.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RecordSentAsync(OmnichannelChannelEndpoint address, string recipient, string messageId, bool isBulk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed send and acts on it: suppresses a dead address, pauses the address's bulk mail on a block or a
    /// throttle.
    /// </summary>
    /// <param name="address">The sending address.</param>
    /// <param name="settings">The address's email settings.</param>
    /// <param name="recipient">The recipient, normalized.</param>
    /// <param name="kind">What the failure means.</param>
    /// <param name="detail">What the server said.</param>
    /// <param name="isBulk">Whether it was bulk mail.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>When the pause the failure started ends, or <see langword="null"/> when it started none.</returns>
    Task<DateTime?> RecordFailureAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, string recipient, EmailFailureKind kind, string detail, bool isBulk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an address's bulk mail. The same push-back reported again while its pause runs belongs to that pause, and
    /// a pause never cuts a longer one short.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="kind">Why. A throttle or a block pauses for longer each time it repeats after its pause ended.</param>
    /// <param name="reason">What happened, for the address's editor.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>When the pause holding the address ends, or <see langword="null"/> for one that lasts until resumed.</returns>
    Task<DateTime?> PauseAsync(string addressId, EmailSendingPauseKind kind, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notes that a receiving domain deferred or soft-bounced mail from an address, and makes bulk mail to that domain
    /// wait once it has done so repeatedly.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="recipientDomain">The receiving domain.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task NoteDomainDeferralAsync(string addressId, string recipientDomain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an address's bulk mail when its bounce or complaint rate has reached the level that gets senders blocked,
    /// and the address asks for that.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="settings">The address's email settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task CheckHealthAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an address's sending health and where it stands against its limits.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="settings">The address's email settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The health.</returns>
    Task<EmailAddressHealth> GetHealthAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends a pause and forgets the run of throttles and blocks that led to it. Resuming a poor-health pause also
    /// measures sending health from now on, since whoever resumes it has cleaned the list of what caused it.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the address was paused.</returns>
    Task<bool> ResumeAsync(string addressId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IEmailSendingGovernor"/>.
/// </summary>
public sealed class EmailSendingGovernor : IEmailSendingGovernor
{
    private static readonly TimeSpan _firstThrottlePause = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan _longestThrottlePause = TimeSpan.FromHours(4);
    private static readonly TimeSpan _firstBlockPause = TimeSpan.FromHours(1);
    private static readonly TimeSpan _longestBlockPause = TimeSpan.FromHours(24);

    // The longest minimum gap between two bulk emails that is waited out in place rather than deferred.
    private static readonly TimeSpan _longestInlineGap = TimeSpan.FromSeconds(30);

    // A poor-health pause lasts until someone resumes; held-back work asks again after this long.
    private static readonly TimeSpan _poorHealthRecheck = TimeSpan.FromHours(1);

    private readonly IEmailDeliveryLog _log;
    private readonly IEmailSuppressionList _suppressions;
    private readonly IEmailSendingStateStore _stateStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    public EmailSendingGovernor(
        IEmailDeliveryLog log,
        IEmailSuppressionList suppressions,
        IEmailSendingStateStore stateStore,
        IClock clock,
        ILogger<EmailSendingGovernor> logger,
        IStringLocalizer<EmailSendingGovernor> stringLocalizer)
    {
        _log = log;
        _suppressions = suppressions;
        _stateStore = stateStore;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task<EmailSendDecision> EvaluateAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, string recipient, EmailSendKind kind, bool reserveTurn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (!string.IsNullOrEmpty(recipient))
        {
            var suppression = await _suppressions.FindAsync(recipient, cancellationToken);

            if (suppression is not null)
            {
                return EmailSendDecision.Refuse(S["{0} is on the email suppression list ({1}), so nothing is sent to it.", recipient, DescribeSuppression(suppression.Reason)]);
            }
        }

        if (kind == EmailSendKind.Reply)
        {
            return EmailSendDecision.Allow();
        }

        var now = _clock.UtcNow;
        var limits = settings?.Limits ?? new EmailSendingLimits();
        var state = await FindStateAsync(address.ItemId, cancellationToken);

        // A pause holds bulk and automated mail back whatever the limits say, and gives out no turns: everything waits for
        // it to end.
        if (state is not null && state.IsPaused(now))
        {
            return Paused(state, now);
        }

        // An automated reply waits only for room under the address's hourly and daily limits. The gap between bulk emails,
        // the per-domain limit and the receiving domains' waits pace bulk mail, and the reply takes no turn, so a customer
        // waiting for an answer is never queued behind a broadcast's backlog.
        if (kind == EmailSendKind.Automated)
        {
            var opening = await GetAddressWindowsOpeningAsync(address.ItemId, limits, now, cancellationToken);

            return opening <= now
                ? EmailSendDecision.Allow()
                : EmailSendDecision.Defer(opening, S["The address is at its sending limit; this automated reply waits for room."]);
        }

        var domain = GetDomain(recipient);

        if (domain is not null && state?.DomainBackoffs is not null && state.DomainBackoffs.TryGetValue(domain, out var domainUntil) && domainUntil > now)
        {
            return EmailSendDecision.Defer(domainUntil, S["{0} asked this address to slow down; mail to it waits.", domain]);
        }

        var gapOpening = await GetGapOpeningAsync(address.ItemId, limits, now, cancellationToken);
        var windowOpening = await GetAddressWindowsOpeningAsync(address.ItemId, limits, now, cancellationToken);

        if (domain is not null)
        {
            windowOpening = Later(windowOpening, await GetWindowOpeningAsync(address.ItemId, limits.MaxPerHourPerDomain, TimeSpan.FromHours(1), domain, now, cancellationToken));
        }

        var earliest = Later(gapOpening, windowOpening);

        if (earliest <= now)
        {
            return EmailSendDecision.Allow();
        }

        // Only the minimum gap between two bulk emails holds this one back, and the gap is a few seconds: the sender
        // waits it out and sends. Deferring would hand the email back to a queue that looks again only a minute later,
        // where it would meet the next due email inside the same gap and go to the back of the line again, so an
        // address would send about one email a minute whatever its limits.
        if (reserveTurn && windowOpening <= now && gapOpening - now <= _longestInlineGap)
        {
            await Task.Delay(gapOpening - now, cancellationToken);

            return EmailSendDecision.Allow();
        }

        var slot = earliest;

        if (reserveTurn)
        {
            var turnInterval = EmailHealthPolicy.GetTurnInterval(limits, now);
            EmailSendingState pausedState = null;

            // The turn is taken on the record as the database holds it now: a turn another process gave out since this one
            // looked is built on rather than overwritten, and a pause a provider's report started since holds the email.
            await UpdateStateAsync(address.ItemId, create: true, fresh =>
            {
                if (fresh.IsPaused(now))
                {
                    pausedState = fresh;

                    return false;
                }

                if (fresh.NextBulkSlotUtc > slot)
                {
                    slot = fresh.NextBulkSlotUtc.Value;
                }

                fresh.NextBulkSlotUtc = slot.Add(turnInterval);

                return true;
            }, cancellationToken);

            if (pausedState is not null)
            {
                return Paused(pausedState, now);
            }
        }

        return EmailSendDecision.Defer(slot, S["The address is at its sending limit; this email waits for its turn."]);
    }

    public async Task RecordSentAsync(OmnichannelChannelEndpoint address, string recipient, string messageId, bool isBulk, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        await _log.RecordAsync(CreateEntry(address.ItemId, EmailDeliveryEventKind.Sent, recipient, messageId, isBulk, detail: null), cancellationToken);

        // Only bulk mail getting through shows the servers take the address's bulk mail again. Replies keep going out
        // while bulk mail is paused, so one of them getting through would end every run of throttles and blocks, and the
        // backoff would never grow.
        if (!isBulk)
        {
            return;
        }

        var state = await FindStateAsync(address.ItemId, cancellationToken);

        if (state is null || (state.ConsecutiveThrottles == 0 && state.ConsecutiveBlocks == 0))
        {
            return;
        }

        var now = _clock.UtcNow;

        await UpdateStateAsync(address.ItemId, create: false, fresh =>
        {
            // A pause running now was started after this email was let go, by mail that failed since: it and its run stay.
            if (fresh.IsPaused(now) || (fresh.ConsecutiveThrottles == 0 && fresh.ConsecutiveBlocks == 0))
            {
                return false;
            }

            // A bulk email that went through ends a run of throttles or blocks, so the next one starts the backoff again.
            fresh.ConsecutiveThrottles = 0;
            fresh.ConsecutiveBlocks = 0;

            if (fresh.PauseKind is EmailSendingPauseKind.Throttled or EmailSendingPauseKind.Blocked)
            {
                ClearPause(fresh);
            }

            return true;
        }, cancellationToken);
    }

    public async Task<DateTime?> RecordFailureAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, string recipient, EmailFailureKind kind, string detail, bool isBulk, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        switch (kind)
        {
            case EmailFailureKind.HardBounce:
                await _log.RecordAsync(CreateEntry(address.ItemId, EmailDeliveryEventKind.HardBounce, recipient, messageId: null, isBulk, detail), cancellationToken);
                await _suppressions.SuppressAsync(recipient, EmailSuppressionReason.HardBounce, detail, cancellationToken: cancellationToken);
                await CheckHealthAsync(address, settings, cancellationToken);

                return null;

            case EmailFailureKind.Blocked:
                await _log.RecordAsync(CreateEntry(address.ItemId, EmailDeliveryEventKind.Blocked, recipient, messageId: null, isBulk, detail), cancellationToken);

                return await PauseAsync(address.ItemId, EmailSendingPauseKind.Blocked, S["a receiving server refused the address's mail: {0}", Shorten(detail)].Value, cancellationToken);

            case EmailFailureKind.Throttled:
                await _log.RecordAsync(CreateEntry(address.ItemId, EmailDeliveryEventKind.Throttled, recipient, messageId: null, isBulk, detail), cancellationToken);

                return await PauseAsync(address.ItemId, EmailSendingPauseKind.Throttled, S["the sending server asked the address to slow down: {0}", Shorten(detail)].Value, cancellationToken);

            default:
                return null;
        }
    }

    public async Task<DateTime?> PauseAsync(string addressId, EmailSendingPauseKind kind, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(addressId);

        var now = _clock.UtcNow;
        DateTime? until = null;
        var started = false;

        await UpdateStateAsync(addressId, create: true, state =>
        {
            var running = state.IsPaused(now) ? state.PauseKind : EmailSendingPauseKind.None;

            // A pause that lasts until someone resumes outranks every other, and is not started twice.
            if (running == EmailSendingPauseKind.PoorHealth)
            {
                return false;
            }

            // The same push-back reported again while its pause runs belongs to that pause: one batch of bounce reports,
            // or mail that was already on its way when the pause began. It neither moves the end nor counts as another
            // time in a row; only push-back after the pause ended does.
            if (running == kind)
            {
                until = state.PausedUntilUtc;

                return false;
            }

            DateTime? next = kind switch
            {
                EmailSendingPauseKind.Throttled => now.Add(Backoff(_firstThrottlePause, _longestThrottlePause, state.ConsecutiveThrottles + 1)),
                EmailSendingPauseKind.Blocked => now.Add(Backoff(_firstBlockPause, _longestBlockPause, state.ConsecutiveBlocks + 1)),
                _ => null,
            };

            // A pause that ends by itself never cuts a longer one short: a throttle during a day-long block leaves the
            // block in place.
            if (next is not null && running != EmailSendingPauseKind.None && state.PausedUntilUtc >= next)
            {
                until = state.PausedUntilUtc;

                return false;
            }

            if (kind == EmailSendingPauseKind.Throttled)
            {
                state.ConsecutiveThrottles++;
            }
            else if (kind == EmailSendingPauseKind.Blocked)
            {
                state.ConsecutiveBlocks++;
            }

            state.PauseKind = kind;
            state.PausedUntilUtc = next;
            state.PausedUtc = now;
            state.PauseReason = reason;
            until = next;
            started = true;

            return true;
        }, cancellationToken);

        if (!started)
        {
            return until;
        }

        _logger.LogWarning(
            "Bulk email from address '{AddressId}' is paused ({PauseKind}) until {PausedUntil}: {Reason}",
            addressId.SanitizeLogValue(),
            kind,
            until?.ToString("u") ?? "it is resumed",
            reason.SanitizeLogValue());

        return until;
    }

    public async Task NoteDomainDeferralAsync(string addressId, string recipientDomain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(addressId) || string.IsNullOrEmpty(recipientDomain))
        {
            return;
        }

        var now = _clock.UtcNow;
        var since = now.Subtract(EmailHealthPolicy.DomainDeferralWindow);

        var deferrals = await _log.CountAsync(addressId, EmailDeliveryEventKind.Deferred, since, recipientDomain, cancellationToken) +
            await _log.CountAsync(addressId, EmailDeliveryEventKind.SoftBounce, since, recipientDomain, cancellationToken);

        if (deferrals < EmailHealthPolicy.DomainDeferralsBeforeBackoff)
        {
            return;
        }

        var until = now.Add(EmailHealthPolicy.DomainBackoff);

        await UpdateStateAsync(addressId, create: true, state =>
        {
            state.DomainBackoffs ??= new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            // Forget the waits that are over, so the record stays small.
            foreach (var expired in state.DomainBackoffs.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
            {
                state.DomainBackoffs.Remove(expired);
            }

            state.DomainBackoffs[recipientDomain] = until;

            return true;
        }, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Bulk email from address '{AddressId}' to {Domain} waits until {Until}: the domain deferred at least {Count} emails in the last {Minutes} minutes.",
                addressId.SanitizeLogValue(),
                recipientDomain.SanitizeLogValue(),
                until,
                EmailHealthPolicy.DomainDeferralsBeforeBackoff,
                (int)EmailHealthPolicy.DomainDeferralWindow.TotalMinutes);
        }
    }

    public async Task CheckHealthAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (settings?.Limits?.PauseOnPoorHealth == false)
        {
            return;
        }

        var now = _clock.UtcNow;
        var state = await FindStateAsync(address.ItemId, cancellationToken);

        if (state?.PauseKind == EmailSendingPauseKind.PoorHealth && state.IsPaused(now))
        {
            return;
        }

        // Health is measured from the last time someone resumed a poor-health pause, when that falls inside the window:
        // the bounces and complaints before it are what the list was cleaned of, and counting them again would pause the
        // address the moment it is resumed.
        var windowStart = now.Subtract(EmailHealthPolicy.Window);
        var sinceResume = state?.ResumedUtc > windowStart;
        var since = sinceResume ? state.ResumedUtc.Value : windowStart;
        var sent = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.Sent, since, cancellationToken: cancellationToken);
        var hardBounces = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.HardBounce, since, cancellationToken: cancellationToken);
        var complaints = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.Complaint, since, cancellationToken: cancellationToken);

        if (!EmailHealthPolicy.ShouldPause(sent, hardBounces, complaints))
        {
            return;
        }

        var reason = sinceResume
            ? S[
                "{0:P1} of the {1} emails sent since sending was resumed bounced and {2:P2} were reported as spam. Clean the list, then resume sending.",
                EmailHealthPolicy.Rate(hardBounces, sent),
                sent,
                EmailHealthPolicy.Rate(complaints, sent)].Value
            : S[
                "{0:P1} of the last 7 days' {1} emails bounced and {2:P2} were reported as spam. Clean the list, then resume sending.",
                EmailHealthPolicy.Rate(hardBounces, sent),
                sent,
                EmailHealthPolicy.Rate(complaints, sent)].Value;

        await PauseAsync(address.ItemId, EmailSendingPauseKind.PoorHealth, reason, cancellationToken);
    }

    public async Task<EmailAddressHealth> GetHealthAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        var now = _clock.UtcNow;
        var since = now.Subtract(EmailHealthPolicy.Window);
        var limits = settings?.Limits ?? new EmailSendingLimits();
        var state = await FindStateAsync(address.ItemId, cancellationToken);

        var health = new EmailAddressHealth
        {
            Sent = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.Sent, since, cancellationToken: cancellationToken),
            HardBounces = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.HardBounce, since, cancellationToken: cancellationToken),
            SoftBounces = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.SoftBounce, since, cancellationToken: cancellationToken),
            Complaints = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.Complaint, since, cancellationToken: cancellationToken),
            Blocks = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.Blocked, since, cancellationToken: cancellationToken),
            SentLastHour = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.Sent, now.AddHours(-1), cancellationToken: cancellationToken),
            SentLastDay = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.Sent, now.AddDays(-1), cancellationToken: cancellationToken),
            DailyLimit = EmailHealthPolicy.GetDailyLimit(limits, now),
            IsWarmingUp = EmailHealthPolicy.IsWarmingUp(limits, now),
            State = state,
            IsPaused = state?.IsPaused(now) == true,
        };

        health.Level = EmailHealthPolicy.Judge(health.Sent, health.HardBounces, health.Complaints);

        return health;
    }

    public async Task<bool> ResumeAsync(string addressId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(addressId))
        {
            return false;
        }

        var now = _clock.UtcNow;
        var wasPaused = false;

        await UpdateStateAsync(addressId, create: false, state =>
        {
            wasPaused = state.IsPaused(now);

            // Whoever resumes a poor-health pause has cleaned the list of what caused it, so health is measured from now.
            // Resuming any other pause leaves the measure alone: it never forgives bounces nobody looked at.
            if (wasPaused && state.PauseKind == EmailSendingPauseKind.PoorHealth)
            {
                state.ResumedUtc = now;
            }

            ClearPause(state);
            state.ConsecutiveThrottles = 0;
            state.ConsecutiveBlocks = 0;
            state.DomainBackoffs?.Clear();

            return true;
        }, cancellationToken);

        if (wasPaused && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Bulk email from address '{AddressId}' was resumed by hand.", addressId.SanitizeLogValue());
        }

        return wasPaused;
    }

    // The earliest the address's limits let one more bulk email leave: the gap after the last one, and the moment an
    // email leaves each rolling window that is full.
    private async Task<DateTime> GetGapOpeningAsync(string addressId, EmailSendingLimits limits, DateTime now, CancellationToken cancellationToken)
    {
        if (limits.MinimumSecondsBetweenSends <= 0)
        {
            return now;
        }

        var lastSent = await _log.GetLastBulkSentUtcAsync(addressId, cancellationToken);

        return lastSent is null ? now : Later(now, lastSent.Value.AddSeconds(limits.MinimumSecondsBetweenSends));
    }

    // The earliest the address's hourly and daily limits, warm-up included, have room for one more email.
    private async Task<DateTime> GetAddressWindowsOpeningAsync(string addressId, EmailSendingLimits limits, DateTime now, CancellationToken cancellationToken)
    {
        var hourly = await GetWindowOpeningAsync(addressId, limits.MaxPerHour, TimeSpan.FromHours(1), domain: null, now, cancellationToken);
        var daily = await GetWindowOpeningAsync(addressId, EmailHealthPolicy.GetDailyLimit(limits, now), TimeSpan.FromDays(1), domain: null, now, cancellationToken);

        return Later(hourly, daily);
    }

    private async Task<DateTime> GetWindowOpeningAsync(string addressId, int limit, TimeSpan window, string domain, DateTime now, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return now;
        }

        var since = now.Subtract(window);
        var count = await _log.CountAsync(addressId, EmailDeliveryEventKind.Sent, since, domain, cancellationToken);

        if (count < limit)
        {
            return now;
        }

        // The window frees a send when the email that brings it back under the limit leaves it.
        var freeing = await _log.GetSentTimeAsync(addressId, since, count - limit, domain, cancellationToken);

        return freeing?.Add(window) ?? now;
    }

    private Task<EmailSendingState> FindStateAsync(string addressId, CancellationToken cancellationToken)
        => _stateStore.FindAsync(addressId, cancellationToken);

    // Changes the address's sending state as the database holds it now, and saves it when the change says it changed
    // something. Every change goes through here, never through a record read to decide: that one may be older than a
    // pause or a turn another process wrote since, and saving it would overwrite them.
    private async Task UpdateStateAsync(string addressId, bool create, Func<EmailSendingState, bool> change, CancellationToken cancellationToken)
    {
        var state = await _stateStore.FindFreshAsync(addressId, cancellationToken);

        if (state is null)
        {
            if (!create)
            {
                return;
            }

            state = new EmailSendingState
            {
                ItemId = UniqueId.GenerateId(),
                AddressId = addressId,
            };
        }

        if (!change(state))
        {
            return;
        }

        state.ModifiedUtc = _clock.UtcNow;

        await _stateStore.SaveAsync(state, cancellationToken);
    }

    private EmailSendDecision Paused(EmailSendingState state, DateTime now)
        => EmailSendDecision.Defer(
            state.PausedUntilUtc ?? now.Add(_poorHealthRecheck),
            S["Bulk sending from this address is paused: {0}", state.PauseReason ?? state.PauseKind.ToString()]);

    private EmailDeliveryLogEntry CreateEntry(string addressId, EmailDeliveryEventKind kind, string recipient, string messageId, bool isBulk, string detail)
        => new()
        {
            AddressId = addressId,
            Kind = kind,
            Recipient = recipient,
            RecipientDomain = GetDomain(recipient),
            MessageId = TrimMessageId(messageId),
            IsBulk = isBulk,
            OccurredUtc = _clock.UtcNow,
            Detail = detail,
        };

    private LocalizedString DescribeSuppression(EmailSuppressionReason reason)
        => reason switch
        {
            EmailSuppressionReason.HardBounce => S["it bounced"],
            EmailSuppressionReason.RepeatedSoftBounces => S["it kept bouncing"],
            EmailSuppressionReason.Complaint => S["its owner reported the business's mail as spam"],
            _ => S["added by hand"],
        };

    private static void ClearPause(EmailSendingState state)
    {
        state.PauseKind = EmailSendingPauseKind.None;
        state.PausedUntilUtc = null;
        state.PausedUtc = null;
        state.PauseReason = null;
    }

    private static TimeSpan Backoff(TimeSpan first, TimeSpan longest, int occurrence)
    {
        var factor = Math.Pow(2, Math.Clamp(occurrence - 1, 0, 16));

        return TimeSpan.FromTicks((long)Math.Min(longest.Ticks, first.Ticks * factor));
    }

    private static DateTime Later(DateTime first, DateTime second)
        => second > first ? second : first;

    private static string Shorten(string detail)
        => string.IsNullOrWhiteSpace(detail) ? "-" : detail.Length > 200 ? detail[..200] : detail;

    internal static string GetDomain(string recipient)
    {
        if (string.IsNullOrEmpty(recipient))
        {
            return null;
        }

        var at = recipient.LastIndexOf('@');

        return at < 0 || at == recipient.Length - 1 ? null : recipient[(at + 1)..].ToLowerInvariant();
    }

    internal static string TrimMessageId(string messageId)
        => string.IsNullOrWhiteSpace(messageId) ? null : messageId.Trim().Trim('<', '>');
}
