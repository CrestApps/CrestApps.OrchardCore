using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.Extensions.Hosting;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Webhooks.Recipes;

/// <summary>
/// Creates and updates webhook subscriptions from a <c>WebhookSubscriptions</c> recipe step. A subscription is matched by
/// its <c>ItemId</c>, so running a recipe twice changes nothing. A subscription may carry its signing secret in
/// <c>Secret</c>; a new subscription without one gets a new secret, and an existing one keeps its own.
/// </summary>
/// <remarks>
/// <code>
/// {
///   "name": "WebhookSubscriptions",
///   "Subscriptions": [
///     { "ItemId": "4k5y9…", "Name": "ERP", "Url": "https://erp.example.com/hooks", "Events": [ "order.paid" ], "IsEnabled": true, "Secret": "whsec_…" }
///   ]
/// }
/// </code>
/// </remarks>
internal sealed class WebhookSubscriptionsStep : CatalogRecipeStepBase<WebhookSubscription>
{
    internal const string Name = "WebhookSubscriptions";

    internal const string EntriesName = "Subscriptions";

    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly WebhookDispatcher _dispatcher;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IClock _clock;

    public WebhookSubscriptionsStep(
        IWebhookSubscriptionStore subscriptions,
        WebhookDispatcher dispatcher,
        IHostEnvironment hostEnvironment,
        IClock clock)
        : base(Name, EntriesName)
    {
        _subscriptions = subscriptions;
        _dispatcher = dispatcher;
        _hostEnvironment = hostEnvironment;
        _clock = clock;
    }

    /// <summary>
    /// Writes a subscription the way this step reads it.
    /// </summary>
    internal static JsonObject Export(WebhookSubscription subscription)
        => new()
        {
            [nameof(WebhookSubscription.ItemId)] = subscription.ItemId,
            [nameof(WebhookSubscription.Name)] = subscription.Name,
            [nameof(WebhookSubscription.Url)] = subscription.Url,
            [nameof(WebhookSubscription.Events)] = new JsonArray([.. (subscription.Events ?? []).Select(entry => JsonValue.Create(entry))]),
            [nameof(WebhookSubscription.IsEnabled)] = subscription.IsEnabled,
        };

    protected override ValueTask<WebhookSubscription> FindByIdAsync(string id)
        => _subscriptions.FindByIdAsync(id);

    protected override ValueTask<WebhookSubscription> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var now = _clock.UtcNow;
        var subscription = new WebhookSubscription
        {
            ItemId = IdGenerator.GenerateId(),
            CreatedUtc = now,
        };

        Apply(subscription, token);
        subscription.ProtectedSecret ??= _dispatcher.NewProtectedSecret();

        return ValueTask.FromResult(subscription);
    }

    protected override async ValueTask UpdateAsync(WebhookSubscription entry, JsonObject token)
    {
        Apply(entry, token);

        if (Validate(entry).Succeeded)
        {
            await _subscriptions.UpdateAsync(entry);
        }
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(WebhookSubscription entry)
        => ValueTask.FromResult(Validate(entry));

    protected override ValueTask CreateAsync(WebhookSubscription entry)
        => _subscriptions.CreateAsync(entry);

    private void Apply(WebhookSubscription subscription, JsonObject token)
    {
        if (token[nameof(WebhookSubscription.Name)]?.GetValue<string>() is { } name)
        {
            subscription.Name = name.Trim();
        }

        if (token[nameof(WebhookSubscription.Url)]?.GetValue<string>() is { } url)
        {
            subscription.Url = url.Trim();
        }

        if (token[nameof(WebhookSubscription.Events)] is JsonArray events)
        {
            subscription.Events = [.. events
                .Select(entry => entry?.GetValue<string>()?.Trim())
                .Where(entry => !string.IsNullOrEmpty(entry))
                .Distinct(StringComparer.Ordinal)];
        }

        if (token[nameof(WebhookSubscription.IsEnabled)] is JsonValue isEnabled && isEnabled.TryGetValue<bool>(out var enabled))
        {
            subscription.IsEnabled = enabled;
        }

        if (token["Secret"]?.GetValue<string>() is { Length: > 0 } secret)
        {
            subscription.ProtectedSecret = _dispatcher.ProtectSecret(secret);
        }

        subscription.ModifiedUtc = _clock.UtcNow;
    }

    private ValidationResultDetails Validate(WebhookSubscription subscription)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrWhiteSpace(subscription.Name))
        {
            result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult($"The webhook subscription '{subscription.ItemId}' needs a Name."));
        }

        if (!EgressNetwork.IsAllowedUrl(subscription.Url, _hostEnvironment.IsDevelopment()))
        {
            result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult($"The webhook subscription '{subscription.Name}' needs a full https:// Url that is not on the site's own network."));
        }

        if (subscription.Events is not { Count: > 0 })
        {
            result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult($"The webhook subscription '{subscription.Name}' needs at least one event, or '{WebhookEvents.All}'."));
        }

        return result;
    }
}
