using System.Text.Json;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Webhooks.Services;

/// <summary>
/// What a person enters for a webhook, on its admin page or through an AI tool. The signing secret is never part of it.
/// </summary>
public sealed class WebhookSubscriptionInput
{
    public string Name { get; set; }

    public string Url { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool AllEvents { get; set; }

    public List<string> Events { get; set; } = [];
}

/// <summary>
/// A problem with what was entered for a webhook: the field it is about and what to do.
/// </summary>
public sealed record WebhookSubscriptionError(string Key, string Message);

/// <summary>
/// The changes the webhooks admin page makes to a subscription, shared with the AI tools: check and copy what was
/// entered, add, give a new signing secret, send a test ping, and delete with its delivery log.
/// </summary>
public sealed class WebhookSubscriptionCommands
{
    /// <summary>
    /// The most deliveries deleting a webhook removes at once; what's left over is cleared with the log's old entries.
    /// </summary>
    public const int DeliveriesDeletedWithWebhook = 1000;

    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IWebhookDeliveryStore _deliveries;
    private readonly WebhookDispatcher _dispatcher;
    private readonly IWebhookEventCatalog _events;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public WebhookSubscriptionCommands(
        IWebhookSubscriptionStore subscriptions,
        IWebhookDeliveryStore deliveries,
        WebhookDispatcher dispatcher,
        IWebhookEventCatalog events,
        IHostEnvironment hostEnvironment,
        IClock clock,
        IStringLocalizer<WebhookSubscriptionCommands> stringLocalizer)
    {
        _subscriptions = subscriptions;
        _deliveries = deliveries;
        _dispatcher = dispatcher;
        _events = events;
        _hostEnvironment = hostEnvironment;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns whether events may be posted to a URL: an absolute https address; plain http only to this machine while
    /// developing.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="isDevelopment">Whether the site runs in development.</param>
    /// <returns><see langword="true"/> when events may be posted there.</returns>
    public static bool IsAllowedUrl(string url, bool isDevelopment)
        => EgressNetwork.IsAllowedUrl(url, isDevelopment);

    /// <summary>
    /// Checks what was entered: a name, an allowed URL, and all events or at least one known event.
    /// </summary>
    /// <param name="input">What was entered.</param>
    /// <returns>The problems, in the order the form shows them.</returns>
    public IReadOnlyList<WebhookSubscriptionError> Validate(WebhookSubscriptionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<WebhookSubscriptionError>();

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add(new(nameof(WebhookSubscriptionInput.Name), S["Name the webhook, such as the system it feeds."]));
        }

        if (!IsAllowedUrl(input.Url, _hostEnvironment.IsDevelopment()))
        {
            errors.Add(new(nameof(WebhookSubscriptionInput.Url), S["Enter the full https:// address events are posted to."]));
        }

        var known = GetKnownEvents();

        if (!input.AllEvents && !(input.Events ?? []).Any(known.Contains))
        {
            errors.Add(new(nameof(WebhookSubscriptionInput.Events), S["Choose the events to send, or all of them."]));
        }

        return errors;
    }

    /// <summary>
    /// Copies what was entered onto a subscription. Events no enabled module declares are dropped.
    /// </summary>
    /// <param name="input">What was entered, already checked.</param>
    /// <param name="subscription">The subscription.</param>
    public void Apply(WebhookSubscriptionInput input, WebhookSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(subscription);

        var known = GetKnownEvents();

        subscription.Name = input.Name.Trim();
        subscription.Url = input.Url.Trim();
        subscription.IsEnabled = input.IsEnabled;
        subscription.Events = input.AllEvents
            ? [WebhookEvents.All]
            : [.. (input.Events ?? []).Where(known.Contains).Distinct(StringComparer.Ordinal)];
        subscription.ModifiedUtc = _clock.UtcNow;
    }

    /// <summary>
    /// Adds a webhook with a new signing secret.
    /// </summary>
    /// <param name="input">What was entered, already checked.</param>
    /// <returns>The new webhook.</returns>
    public async Task<WebhookSubscription> CreateAsync(WebhookSubscriptionInput input)
    {
        var subscription = new WebhookSubscription
        {
            ItemId = IdGenerator.GenerateId(),
            ProtectedSecret = _dispatcher.NewProtectedSecret(),
            CreatedUtc = _clock.UtcNow,
        };

        Apply(input, subscription);
        await _subscriptions.CreateAsync(subscription);

        return subscription;
    }

    /// <summary>
    /// Saves what was entered on an existing webhook. Its signing secret is kept.
    /// </summary>
    /// <param name="subscription">The webhook.</param>
    /// <param name="input">What was entered, already checked.</param>
    /// <returns>A task that completes when it is saved.</returns>
    public async Task UpdateAsync(WebhookSubscription subscription, WebhookSubscriptionInput input)
    {
        Apply(input, subscription);
        await _subscriptions.UpdateAsync(subscription);
    }

    /// <summary>
    /// Gives a webhook a new signing secret; deliveries are signed with it from then on.
    /// </summary>
    /// <param name="subscription">The webhook.</param>
    /// <returns>A task that completes when it is saved.</returns>
    public async Task RotateSecretAsync(WebhookSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        subscription.ProtectedSecret = _dispatcher.NewProtectedSecret();
        subscription.ModifiedUtc = _clock.UtcNow;
        await _subscriptions.UpdateAsync(subscription);
    }

    /// <summary>
    /// Logs a <c>ping</c> delivery for a webhook and sends it at once, to try the receiver.
    /// </summary>
    /// <param name="subscription">The webhook.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The delivery, with what the attempt came to.</returns>
    public async Task<WebhookDelivery> PingAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var now = _clock.UtcNow;
        var eventId = "evt_" + IdGenerator.GenerateId();
        var delivery = new WebhookDelivery
        {
            ItemId = IdGenerator.GenerateId(),
            SubscriptionId = subscription.ItemId,
            EventId = eventId,
            EventName = WebhookEvents.Ping,
            Payload = JsonSerializer.Serialize(new { id = eventId, @event = WebhookEvents.Ping, createdUtc = now, data = new { webhookId = subscription.ItemId, name = subscription.Name } }, JsonSerializerOptions.Web),
            Status = WebhookDeliveryStatus.Pending,
            NextAttemptUtc = now,
            CreatedUtc = now,
        };

        await _deliveries.CreateAsync(delivery, cancellationToken);
        await _dispatcher.SendAsync(delivery, cancellationToken);

        return delivery;
    }

    /// <summary>
    /// Deletes a webhook, then its delivery log. Nothing more is sent to it.
    /// </summary>
    /// <param name="subscription">The webhook.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when both are deleted.</returns>
    public async Task DeleteAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await _subscriptions.DeleteAsync(subscription, cancellationToken);

        // Its log goes with it; what's left over is cleared with the log's old entries.
        await _deliveries.DeleteBySubscriptionAsync(subscription.ItemId, take: DeliveriesDeletedWithWebhook, cancellationToken);
    }

    private HashSet<string> GetKnownEvents()
        => _events.GetEvents().Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
}
