using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IWebPushService"/>. The site's VAPID key pair is made the first time it is asked for and
/// kept in the Workforce collection, its private key protected with data protection. A device's endpoint is where the
/// site posts notifications, so only a public HTTPS address is accepted (and, when <see cref="WebPushOptions.AllowedHosts"/>
/// lists push services, only theirs), and a worker keeps at most <see cref="WebPushOptions.MaxSubscriptionsPerWorker"/>
/// devices: one more replaces the oldest.
/// </summary>
internal sealed class DefaultWebPushService : IWebPushService
{
    private const string Purpose = "CrestApps.Workforce.WebPush";

    private readonly ISession _session;
    private readonly WebPushSubscriptionStore _store;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly IClock _clock;
    private readonly WebPushOptions _options;

    public DefaultWebPushService(ISession session, WebPushSubscriptionStore store, IDataProtectionProvider dataProtection, IClock clock, IOptions<WebPushOptions> options)
    {
        _session = session;
        _store = store;
        _dataProtection = dataProtection;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<string> GetPublicKeyAsync(CancellationToken cancellationToken = default)
    {
        return (await GetKeysAsync(cancellationToken)).PublicKey;
    }

    /// <summary>
    /// Returns the key pair, making it the first time.
    /// </summary>
    public async Task<(string PublicKey, string PrivateKey)> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        var protector = _dataProtection.CreateProtector(Purpose);
        var keys = await _session.Query<WorkforcePushKeys>(collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

        if (keys is not null)
        {
            return (keys.PublicKey, protector.Unprotect(keys.ProtectedPrivateKey));
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(true);
        var publicKey = PortalSecrets.Base64Url([0x04, .. parameters.Q.X, .. parameters.Q.Y]);
        var privateKey = PortalSecrets.Base64Url(parameters.D);

        await _session.SaveAsync(new WorkforcePushKeys { PublicKey = publicKey, ProtectedPrivateKey = protector.Protect(privateKey) }, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

        // Saved now, so a second request at the same time reads this pair rather than making its own.
        await _session.FlushAsync(cancellationToken);

        return (publicKey, privateKey);
    }

    public async Task<WebPushSubscription> SubscribeAsync(string workerId, string endpoint, string p256dh, string auth, string userAgent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        ArgumentException.ThrowIfNullOrEmpty(endpoint);

        if (!IsAllowedEndpoint(endpoint))
        {
            return null;
        }

        var subscription = await _store.FindAsync(workerId, endpoint, cancellationToken);
        var isNew = subscription is null;

        subscription ??= new WebPushSubscription
        {
            ItemId = UniqueId.GenerateId(),
            WorkerId = workerId,
            Endpoint = endpoint,
            CreatedUtc = _clock.UtcNow,
        };

        subscription.P256dh = p256dh;
        subscription.Auth = auth;
        subscription.UserAgent = userAgent?.Length > 200 ? userAgent[..200] : userAgent;

        if (isNew)
        {
            await _store.CreateAsync(subscription, cancellationToken);
        }
        else
        {
            await _store.UpdateAsync(subscription, cancellationToken);
        }

        // Saved now, so a repeat of the same subscription finds this one.
        await _session.FlushAsync(cancellationToken);

        if (isNew)
        {
            await EvictOldestAsync(workerId, cancellationToken);
        }

        return subscription;
    }

    /// <summary>
    /// Gets whether the site may post notifications to an endpoint: a public HTTPS address, never one on the site's own
    /// network, and one of the allowed push services when they are listed.
    /// </summary>
    internal bool IsAllowedEndpoint(string endpoint)
    {
        if (!EgressNetwork.IsAllowedUrl(endpoint, isDevelopment: false) ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var allowed = _options.AllowedHosts?.Where(host => !string.IsNullOrWhiteSpace(host)).ToArray() ?? [];

        return allowed.Length == 0 || allowed.Any(host => MatchesHost(uri.IdnHost, host.Trim()));
    }

    private static bool MatchesHost(string host, string allowed)
        => allowed.StartsWith("*.", StringComparison.Ordinal)
            ? host.EndsWith(allowed[1..], StringComparison.OrdinalIgnoreCase)
            : string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase);

    // A worker keeps a few devices; the oldest goes when one more is added, so one worker can't turn a notification into
    // a flood of outbound requests.
    private async Task EvictOldestAsync(string workerId, CancellationToken cancellationToken)
    {
        var limit = Math.Max(1, _options.MaxSubscriptionsPerWorker);
        var subscriptions = await _store.ListAsync(workerId, cancellationToken);

        if (subscriptions.Count <= limit)
        {
            return;
        }

        foreach (var oldest in subscriptions.OrderByDescending(item => item.CreatedUtc).ThenByDescending(item => item.ItemId, StringComparer.Ordinal).Skip(limit))
        {
            await _store.DeleteAsync(oldest, cancellationToken);
        }

        await _session.FlushAsync(cancellationToken);
    }

    public async Task<bool> UnsubscribeAsync(string workerId, string endpoint, CancellationToken cancellationToken = default)
    {
        var subscription = await _store.FindAsync(workerId, endpoint, cancellationToken);

        if (subscription is null)
        {
            return false;
        }

        await _store.DeleteAsync(subscription, cancellationToken);

        return true;
    }

    public Task<IReadOnlyList<WebPushSubscription>> ListAsync(string workerId, CancellationToken cancellationToken = default)
    {
        return _store.ListAsync(workerId, cancellationToken);
    }
}
