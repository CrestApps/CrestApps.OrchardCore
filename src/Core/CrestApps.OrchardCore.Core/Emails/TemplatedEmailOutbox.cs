using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// The default <see cref="ITemplatedEmailOutbox"/>. An email is built in its culture and rendered once with the site's
/// template of its kind, then tried at once through Orchard Core's <see cref="IEmailService"/> and kept, in the caller's
/// unit of work, as sent or as waiting. A waiting email is tried again by the <see cref="TemplatedEmailOutboxBackgroundTask"/>
/// after 5 minutes, 15 minutes, an hour, 4 hours and 12 hours, then given up. Each attempt is told to the
/// <see cref="ITemplatedEmailOutboxObserver"/>s; sent and given-up emails are removed after
/// <see cref="TemplatedEmailOutboxRetentionPolicy.RetentionDays"/> days.
/// </summary>
public sealed class TemplatedEmailOutbox : ITemplatedEmailOutbox
{
    /// <summary>
    /// How many attempts an email gets in all: the first one and five retries.
    /// </summary>
    public const int MaxAttempts = 6;

    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan[] _retryDelays =
    [
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4),
        TimeSpan.FromHours(12),
    ];

    private readonly ISession _session;
    private readonly TemplatedEmailOutboxQueue _queue;
    private readonly IEmailService _emailService;
    private readonly ITemplatedEmailTemplates _templates;
    private readonly ITemplatedEmailOutboxObserver[] _observers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatedEmailOutbox"/> class.
    /// </summary>
    /// <param name="store">The tenant store, which claims and settlements open their own sessions on.</param>
    /// <param name="session">The caller's session, which new emails are kept in.</param>
    /// <param name="emailServices">The email service, when the Email feature is on; none otherwise.</param>
    /// <param name="templates">The site's templates of its emails.</param>
    /// <param name="observers">The observers of the emails' attempts.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TemplatedEmailOutbox(
        IStore store,
        ISession session,
        IEnumerable<IEmailService> emailServices,
        ITemplatedEmailTemplates templates,
        IEnumerable<ITemplatedEmailOutboxObserver> observers,
        IClock clock,
        ILogger<TemplatedEmailOutbox> logger,
        IStringLocalizer<TemplatedEmailOutbox> stringLocalizer)
    {
        _session = session;
        _queue = new TemplatedEmailOutboxQueue(store, session, clock);
        _emailService = emailServices?.FirstOrDefault();
        _templates = templates;
        _observers = observers?.ToArray() ?? [];
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the waits before each retry: after the first failed attempt, after the second, and so on.
    /// </summary>
    internal static IReadOnlyList<TimeSpan> RetryDelays
        => _retryDelays;

    /// <inheritdoc/>
    /// <remarks>
    /// The email is <see cref="TemplatedEmailOutboxStatus.Skipped"/>, and nothing is kept, when the site has no email
    /// service (the Email feature is off), the email has no recipient, or <paramref name="buildAsync"/> returns
    /// <see langword="null"/>, which is how a sender says the kind is off. The site's template of a kind that is not
    /// turned on in its settings only means the built email goes as it is.
    /// </remarks>
    public async Task<TemplatedEmailOutboxResult> SendAsync(TemplatedEmail email, Func<CancellationToken, Task<TemplatedEmailContent>> buildAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(buildAsync);

        if (string.IsNullOrWhiteSpace(email.To) || _emailService is null)
        {
            return Skipped();
        }

        TemplatedEmailMessage message;

        using (TemplatedEmailCulture.Use(email.Culture))
        {
            var content = await buildAsync(cancellationToken);

            if (content is null)
            {
                return Skipped();
            }

            message = await RenderAsync(email, content, cancellationToken);
        }

        var now = _clock.UtcNow;
        var item = new TemplatedEmailOutboxItem
        {
            ItemId = UniqueId.GenerateId(),
            Kind = email.Kind,
            To = email.To.Trim(),
            Culture = email.Culture,
            Reference = email.Reference,
            Source = email.Source,
            EmailProperties = email.Properties is null ? new(StringComparer.Ordinal) : new(email.Properties, StringComparer.Ordinal),
            Subject = message.Subject,
            Text = message.Text,
            Html = message.Html,
            CreatedUtc = now,
        };

        var error = await TrySendAsync(item, cancellationToken);

        if (error is null)
        {
            // Kept as sent, so the record it belongs to can find it until the retention policy removes it.
            item.Status = LeasedWorkItemStatus.Completed;
            item.NextAttemptUtc = now;
            item.SentUtc = now;

            await _session.SaveAsync(item, checkConcurrency: true, collection: TemplatedEmailOutboxStorage.CollectionName, cancellationToken: cancellationToken);
            await NotifyAsync(item, static (observer, delivery, token) => observer.DeliveredAsync(delivery, token), cancellationToken);

            return new TemplatedEmailOutboxResult
            {
                ItemId = item.ItemId,
                Status = TemplatedEmailOutboxStatus.Sent,
            };
        }

        // The first attempt counts: the background task tries it again after the first wait.
        item.AttemptCount = 1;
        item.LastError = error;
        item.NextAttemptUtc = now.Add(GetRetryDelay(1));

        await _queue.EnqueueAsync(item, cancellationToken);
        await NotifyAsync(item, static (observer, delivery, token) => observer.RetryingAsync(delivery, token), cancellationToken);

        return new TemplatedEmailOutboxResult
        {
            ItemId = item.ItemId,
            Status = TemplatedEmailOutboxStatus.Queued,
        };
    }

    /// <summary>
    /// Returns the wait before the next attempt of an email whose attempts failed so many times.
    /// </summary>
    /// <param name="failedAttempts">How many attempts failed, from 1.</param>
    /// <returns>The wait.</returns>
    internal static TimeSpan GetRetryDelay(int failedAttempts)
        => _retryDelays[Math.Clamp(failedAttempts, 1, _retryDelays.Length) - 1];

    /// <summary>
    /// Tries the emails whose next attempt is due, each claimed just before it is tried and settled on its own, so a
    /// slow email service never lets the lease of an email still waiting in the batch run out (another node would then
    /// claim and send it too).
    /// </summary>
    /// <param name="batchSize">The most emails to try.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>What became of the emails.</returns>
    internal async Task<TemplatedEmailOutboxRun> SendDueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var claimed = 0;
        var sent = 0;
        var retried = 0;
        var abandoned = 0;

        for (var index = 0; index < batchSize; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var next = await _queue.ClaimDueAsync(1, _lease, cancellationToken);

            if (next.Count == 0)
            {
                break;
            }

            claimed++;

            var item = next[0];
            var error = await TrySendAsync(item, cancellationToken);
            TemplatedEmailOutboxItem settled = null;

            if (error is null)
            {
                var sentUtc = _clock.UtcNow;

                if (await _queue.CompleteAsync(item, stored =>
                {
                    stored.SentUtc = sentUtc;
                    settled = stored;
                }, cancellationToken))
                {
                    sent++;
                    await NotifyAsync(settled, static (observer, delivery, token) => observer.DeliveredAsync(delivery, token), cancellationToken);
                }

                continue;
            }

            var status = await _queue.RetryAsync(item, error, GetRetryDelay, update: stored =>
            {
                if (stored.Status == LeasedWorkItemStatus.DeadLettered)
                {
                    stored.AbandonedUtc = _clock.UtcNow;
                }

                settled = stored;
            }, cancellationToken: cancellationToken);

            if (status == LeasedWorkItemStatus.DeadLettered)
            {
                abandoned++;

                _logger.LogWarning(
                    "The {Kind} e-mail about '{Reference}' ({ItemId}) wasn't delivered after {Attempts} attempts: {Error}",
                    settled.Kind,
                    settled.Reference.SanitizeLogValue(),
                    settled.ItemId,
                    settled.AttemptCount,
                    settled.LastError.SanitizeLogValue());

                await NotifyAsync(settled, static (observer, delivery, token) => observer.AbandonedAsync(delivery, token), cancellationToken);
            }
            else if (status == LeasedWorkItemStatus.Pending)
            {
                retried++;
                await NotifyAsync(settled, static (observer, delivery, token) => observer.RetryingAsync(delivery, token), cancellationToken);
            }
        }

        return new TemplatedEmailOutboxRun(claimed, sent, retried, abandoned);
    }

    /// <summary>
    /// Describes a kept email as the observers see it.
    /// </summary>
    /// <param name="item">The kept email.</param>
    /// <returns>The delivery.</returns>
    internal static TemplatedEmailDelivery ToDelivery(TemplatedEmailOutboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var status = item.Status switch
        {
            LeasedWorkItemStatus.Completed => TemplatedEmailOutboxStatus.Sent,
            LeasedWorkItemStatus.DeadLettered => TemplatedEmailOutboxStatus.Abandoned,
            _ => TemplatedEmailOutboxStatus.Queued,
        };

        return new TemplatedEmailDelivery
        {
            ItemId = item.ItemId,
            Kind = item.Kind,
            To = item.To,
            Reference = item.Reference,
            Source = item.Source,
            Properties = item.EmailProperties ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Status = status,
            FailedAttempts = item.AttemptCount,
            LastError = status == TemplatedEmailOutboxStatus.Sent ? null : item.LastError,
            NextAttemptUtc = status == TemplatedEmailOutboxStatus.Queued ? item.NextAttemptUtc : null,
        };
    }

    private static TemplatedEmailOutboxResult Skipped()
        => new()
        {
            Status = TemplatedEmailOutboxStatus.Skipped,
        };

    private async Task<TemplatedEmailMessage> RenderAsync(TemplatedEmail email, TemplatedEmailContent content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(email.Kind) || _templates is null)
        {
            return new TemplatedEmailMessage(content.Subject, content.Text, content.Html);
        }

        return await _templates.ApplyAsync(new TemplatedEmailContext
        {
            Kind = email.Kind,
            Models = email.Models ?? new Dictionary<string, object>(StringComparer.Ordinal),
            Subject = content.Subject,
            Text = content.Text,
            Html = content.Html,
            Values = content.Values ?? new Dictionary<string, string>(),
        }, cancellationToken);
    }

    // Returns null when the email service took the email, and why it didn't otherwise.
    private async Task<string> TrySendAsync(TemplatedEmailOutboxItem item, CancellationToken cancellationToken)
    {
        if (_emailService is null)
        {
            return S["The site can't send e-mail; turn on the Email feature and set it up."];
        }

        try
        {
            var result = await _emailService.SendAsync(new MailMessage
            {
                To = item.To,
                Subject = item.Subject,
                TextBody = item.Text,
                HtmlBody = item.Html,
            }, cancellationToken: cancellationToken);

            if (result.Succeeded)
            {
                return null;
            }

            var errors = string.Join(" ", (result.Errors ?? []).Select(error => error.Message).Where(message => !string.IsNullOrWhiteSpace(message)));

            return string.IsNullOrEmpty(errors)
                ? S["The e-mail service didn't take it; check that the site's e-mail is set up."]
                : errors;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A mail server that's down never undoes what the email is about; the email waits for its next attempt.
            _logger.LogError(exception, "The {Kind} e-mail about '{Reference}' ({ItemId}) failed.", item.Kind, item.Reference.SanitizeLogValue(), item.ItemId);

            return exception.Message;
        }
    }

    private async Task NotifyAsync(
        TemplatedEmailOutboxItem item,
        Func<ITemplatedEmailOutboxObserver, TemplatedEmailDelivery, CancellationToken, Task> callback,
        CancellationToken cancellationToken)
    {
        if (_observers.Length == 0 || item is null)
        {
            return;
        }

        var delivery = ToDelivery(item);

        foreach (var observer in _observers)
        {
            try
            {
                await callback(observer, delivery, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "An observer of the templated e-mail outbox failed for the e-mail {ItemId}.", item.ItemId);
            }
        }
    }
}

/// <summary>
/// What became of the emails one pass of the outbox tried.
/// </summary>
/// <param name="Claimed">How many emails the pass claimed.</param>
/// <param name="Sent">How many the email service took.</param>
/// <param name="Retried">How many wait for another attempt.</param>
/// <param name="Abandoned">How many were given up.</param>
internal readonly record struct TemplatedEmailOutboxRun(int Claimed, int Sent, int Retried, int Abandoned);
