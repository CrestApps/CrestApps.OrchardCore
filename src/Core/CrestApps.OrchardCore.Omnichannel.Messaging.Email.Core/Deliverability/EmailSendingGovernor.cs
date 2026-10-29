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
    /// Decides whether one email may leave <paramref name="address"/>. Replies are only checked against the suppression
    /// list; bulk mail is also held to the address's limits and pauses.
    /// </summary>
    /// <param name="address">The sending address.</param>
    /// <param name="settings">The address's email settings.</param>
    /// <param name="recipient">The recipient, normalized, or <see langword="null"/> when asking about the address alone.</param>
    /// <param name="isBulk">Whether the email is bulk mail.</param>
    /// <param name="reserveTurn">Whether a held-back caller will wait for the time returned, so the next one gets a later turn.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The decision.</returns>
    Task<EmailSendDecision> EvaluateAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, string recipient, bool isBulk, bool reserveTurn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an email the sending server accepted.
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
    /// Pauses an address's bulk mail.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="kind">Why. A throttle or a block pauses for longer each time it repeats.</param>
    /// <param name="reason">What happened, for the address's editor.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>When the pause ends, or <see langword="null"/> for one that lasts until resumed.</returns>
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
    /// Ends a pause and forgets the run of throttles and blocks that led to it.
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

    public async Task<EmailSendDecision> EvaluateAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, string recipient, bool isBulk, bool reserveTurn, CancellationToken cancellationToken = default)
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

        if (!isBulk)
        {
            return EmailSendDecision.Allow();
        }

        var now = _clock.UtcNow;
        var limits = settings?.Limits ?? new EmailSendingLimits();
        var state = await FindStateAsync(address.ItemId, cancellationToken);

        // A pause holds bulk mail back whatever the limits say, and gives out no turns: everything waits for it to end.
        if (state is not null && state.IsPaused(now))
        {
            var until = state.PausedUntilUtc ?? now.Add(_poorHealthRecheck);

            return EmailSendDecision.Defer(until, S["Bulk sending from this address is paused: {0}", state.PauseReason ?? state.PauseKind.ToString()]);
        }

        var domain = GetDomain(recipient);

        if (domain is not null && state?.DomainBackoffs is not null && state.DomainBackoffs.TryGetValue(domain, out var domainUntil) && domainUntil > now)
        {
            return EmailSendDecision.Defer(domainUntil, S["{0} asked this address to slow down; mail to it waits.", domain]);
        }

        var earliest = await GetEarliestSendTimeAsync(address.ItemId, limits, domain, now, cancellationToken);

        if (earliest <= now)
        {
            return EmailSendDecision.Allow();
        }

        var slot = earliest;

        if (reserveTurn)
        {
            state ??= await GetOrCreateStateAsync(address.ItemId, cancellationToken);

            if (state.NextBulkSlotUtc > slot)
            {
                slot = state.NextBulkSlotUtc.Value;
            }

            state.NextBulkSlotUtc = slot.Add(EmailHealthPolicy.GetTurnInterval(limits, now));
            await SaveStateAsync(state, cancellationToken);
        }

        return EmailSendDecision.Defer(slot, S["The address is at its sending limit; this email waits for its turn."]);
    }

    public async Task RecordSentAsync(OmnichannelChannelEndpoint address, string recipient, string messageId, bool isBulk, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        await _log.RecordAsync(CreateEntry(address.ItemId, EmailDeliveryEventKind.Sent, recipient, messageId, isBulk, detail: null), cancellationToken);

        var state = await FindStateAsync(address.ItemId, cancellationToken);

        // A send that went through ends a run of throttles or blocks, so the next one starts the backoff again.
        if (state is not null && (state.ConsecutiveThrottles > 0 || state.ConsecutiveBlocks > 0))
        {
            state.ConsecutiveThrottles = 0;
            state.ConsecutiveBlocks = 0;

            if (state.PauseKind is EmailSendingPauseKind.Throttled or EmailSendingPauseKind.Blocked && !state.IsPaused(_clock.UtcNow))
            {
                ClearPause(state);
            }

            await SaveStateAsync(state, cancellationToken);
        }
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
        var state = await GetOrCreateStateAsync(addressId, cancellationToken);

        // A pause that lasts until someone resumes outranks one that ends by itself.
        if (state.PauseKind == EmailSendingPauseKind.PoorHealth && kind != EmailSendingPauseKind.PoorHealth && state.IsPaused(now))
        {
            return null;
        }

        DateTime? until = kind switch
        {
            EmailSendingPauseKind.Throttled => now.Add(Backoff(_firstThrottlePause, _longestThrottlePause, ++state.ConsecutiveThrottles)),
            EmailSendingPauseKind.Blocked => now.Add(Backoff(_firstBlockPause, _longestBlockPause, ++state.ConsecutiveBlocks)),
            _ => null,
        };

        state.PauseKind = kind;
        state.PausedUntilUtc = until;
        state.PausedUtc = now;
        state.PauseReason = reason;

        await SaveStateAsync(state, cancellationToken);

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

        var state = await GetOrCreateStateAsync(addressId, cancellationToken);

        state.DomainBackoffs ??= new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        // Forget the waits that are over, so the record stays small.
        foreach (var expired in state.DomainBackoffs.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
        {
            state.DomainBackoffs.Remove(expired);
        }

        state.DomainBackoffs[recipientDomain] = now.Add(EmailHealthPolicy.DomainBackoff);

        await SaveStateAsync(state, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Bulk email from address '{AddressId}' to {Domain} waits until {Until}: the domain deferred at least {Count} emails in the last {Minutes} minutes.",
                addressId.SanitizeLogValue(),
                recipientDomain.SanitizeLogValue(),
                state.DomainBackoffs[recipientDomain],
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

        var since = _clock.UtcNow.Subtract(EmailHealthPolicy.Window);
        var sent = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.Sent, since, cancellationToken: cancellationToken);
        var hardBounces = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.HardBounce, since, cancellationToken: cancellationToken);
        var complaints = await _log.CountAsync(address.ItemId, EmailDeliveryEventKind.Complaint, since, cancellationToken: cancellationToken);

        if (!EmailHealthPolicy.ShouldPause(sent, hardBounces, complaints))
        {
            return;
        }

        var state = await FindStateAsync(address.ItemId, cancellationToken);

        if (state?.PauseKind == EmailSendingPauseKind.PoorHealth && state.IsPaused(_clock.UtcNow))
        {
            return;
        }

        var reason = S[
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
        var state = await FindStateAsync(addressId, cancellationToken);

        if (state is null)
        {
            return false;
        }

        var wasPaused = state.IsPaused(_clock.UtcNow);

        ClearPause(state);
        state.ConsecutiveThrottles = 0;
        state.ConsecutiveBlocks = 0;
        state.DomainBackoffs?.Clear();

        await SaveStateAsync(state, cancellationToken);

        if (wasPaused && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Bulk email from address '{AddressId}' was resumed by hand.", addressId.SanitizeLogValue());
        }

        return wasPaused;
    }

    // The earliest the address's limits let one more bulk email leave: the gap after the last one, and the moment an
    // email leaves each rolling window that is full.
    private async Task<DateTime> GetEarliestSendTimeAsync(string addressId, EmailSendingLimits limits, string domain, DateTime now, CancellationToken cancellationToken)
    {
        var earliest = now;

        if (limits.MinimumSecondsBetweenSends > 0)
        {
            var lastSent = await _log.GetLastBulkSentUtcAsync(addressId, cancellationToken);

            if (lastSent is not null)
            {
                earliest = Later(earliest, lastSent.Value.AddSeconds(limits.MinimumSecondsBetweenSends));
            }
        }

        earliest = Later(earliest, await GetWindowOpeningAsync(addressId, limits.MaxPerHour, TimeSpan.FromHours(1), domain: null, now, cancellationToken));
        earliest = Later(earliest, await GetWindowOpeningAsync(addressId, EmailHealthPolicy.GetDailyLimit(limits, now), TimeSpan.FromDays(1), domain: null, now, cancellationToken));

        if (domain is not null)
        {
            earliest = Later(earliest, await GetWindowOpeningAsync(addressId, limits.MaxPerHourPerDomain, TimeSpan.FromHours(1), domain, now, cancellationToken));
        }

        return earliest;
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

    private async Task<EmailSendingState> GetOrCreateStateAsync(string addressId, CancellationToken cancellationToken)
    {
        var state = await FindStateAsync(addressId, cancellationToken);

        if (state is null)
        {
            state = new EmailSendingState
            {
                ItemId = UniqueId.GenerateId(),
                AddressId = addressId,
            };
        }

        return state;
    }

    private async Task SaveStateAsync(EmailSendingState state, CancellationToken cancellationToken)
    {
        state.ModifiedUtc = _clock.UtcNow;

        await _stateStore.SaveAsync(state, cancellationToken);
    }

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
