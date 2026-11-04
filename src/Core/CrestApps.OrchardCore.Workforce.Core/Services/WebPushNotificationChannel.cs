using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Sends notifications to the devices a worker turned push on for, encrypted for each device and signed with the
/// site's VAPID key. A device the push service says is gone is forgotten.
/// </summary>
internal sealed class WebPushNotificationChannel : IWorkforceNotificationChannel
{
    private readonly WebPushSubscriptionStore _store;
    private readonly DefaultWebPushService _pushService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public WebPushNotificationChannel(
        WebPushSubscriptionStore store,
        DefaultWebPushService pushService,
        IHttpClientFactory httpClientFactory,
        ISiteService siteService,
        IClock clock,
        ILogger<WebPushNotificationChannel> logger,
        IStringLocalizer<WebPushNotificationChannel> stringLocalizer)
    {
        _store = store;
        _pushService = pushService;
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public string Name => "Push";

    public string DisplayName => S["Push notifications"];

    public async Task SendAsync(WorkerProfile worker, WorkforceNotification notification, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _store.ListAsync(worker.ItemId, cancellationToken);

        if (subscriptions.Count == 0)
        {
            return;
        }

        var (publicKey, privateKey) = await _pushService.GetKeysAsync(cancellationToken);
        var baseUrl = (await _siteService.GetSiteSettingsAsync()).BaseUrl;
        var client = new PushServiceClient(_httpClientFactory.CreateClient(WorkforceConstants.WebPushHttpClient))
        {
            DefaultAuthentication = new VapidAuthentication(publicKey, privateKey)
            {
                Subject = Uri.TryCreate(baseUrl, UriKind.Absolute, out var site) && site.Scheme == Uri.UriSchemeHttps ? site.GetLeftPart(UriPartial.Authority) : "mailto:workforce@localhost",
            },
        };
        var message = new PushMessage(JsonSerializer.Serialize(new { title = notification.Title, body = notification.Body, url = "/" + (notification.Url ?? "my-work").TrimStart('/') }))
        {
            TimeToLive = 24 * 60 * 60,
            Urgency = PushMessageUrgency.Normal,
        };

        foreach (var subscription in subscriptions)
        {
            var target = new PushSubscription { Endpoint = subscription.Endpoint };

            target.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dh);
            target.SetKey(PushEncryptionKeyName.Auth, subscription.Auth);

            try
            {
                await client.RequestPushMessageDeliveryAsync(target, message, cancellationToken);
                subscription.LastSentUtc = _clock.UtcNow;
                await _store.UpdateAsync(subscription, cancellationToken);
            }
            catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                await _store.DeleteAsync(subscription, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or PushServiceClientException or ArgumentException)
            {
                _logger.LogWarning(ex, "A push notification could not reach a device.");
            }
        }
    }
}
