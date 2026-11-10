using System.Security.Cryptography;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Downloads.Services;

/// <summary>
/// Gives buyers the files of the digital products they bought, and takes them back.
/// </summary>
/// <remarks>
/// A paid order gets one <see cref="DownloadEntitlement"/> per file of each product it bought. Its link is the
/// entitlement's id signed with data protection, so a link can be shown again on the order page or in a resent email
/// and nothing secret is stored; whether it still works is decided by the entitlement: its download count, expiry and
/// whether it was revoked.
/// </remarks>
public sealed class DownloadService
{
    private const string Collection = DownloadsConstants.CollectionName;
    private const string ProtectionPurpose = "CrestApps.OrchardCore.Downloads.Link";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly ISession _session;
    private readonly ISiteService _siteService;
    private readonly IDataProtector _protector;
    private readonly SiteUrlGenerator _siteUrlGenerator;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    public DownloadService(
        ISession session,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        SiteUrlGenerator siteUrlGenerator,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _session = session;
        _siteService = siteService;
        _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose);
        _siteUrlGenerator = siteUrlGenerator;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <summary>
    /// Returns the IDs of the order's lines that already have files.
    /// </summary>
    /// <param name="orderId">The order's ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<ISet<string>> GetGrantedLineIdsAsync(string orderId, CancellationToken cancellationToken = default)
        => (await ListAsync(orderId, cancellationToken))
            .Where(entitlement => entitlement.OrderLineId is not null)
            .Select(entitlement => entitlement.OrderLineId)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Gives a paid order's buyer the files of one line's digital product, as they were when it was ordered:
    /// <paramref name="part"/> comes from the purchased version. Granting again adds only what is missing.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <param name="line">The line.</param>
    /// <param name="part">The digital files of the purchased version of the line's product.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many files were given.</returns>
    public async Task<int> GrantLineAsync(Order order, OrderLine line, DigitalFilesPart part, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(line);

        if (part?.Files is not { Count: > 0 })
        {
            return 0;
        }

        var settings = await _siteService.GetSettingsAsync<DownloadSettings>();
        var existing = await ListAsync(order.ItemId, cancellationToken);
        var now = _clock.UtcNow;
        var limit = part.DownloadLimit ?? settings.DownloadLimit;
        var days = part.LinkLifetimeDays ?? settings.LinkLifetimeDays;
        var granted = 0;

        foreach (var file in part.Files)
        {
            if (existing.Any(entitlement => entitlement.OrderLineId == line.LineId && entitlement.FileId == file.FileId))
            {
                continue;
            }

            await _session.SaveAsync(new DownloadEntitlement
            {
                ItemId = IdGenerator.GenerateId(),
                OrderId = order.ItemId,
                OrderNumber = order.OrderNumber,
                OrderLineId = line.LineId,
                ProductId = line.ContentItemId,
                ProductTitle = line.Title,
                FileId = file.FileId,
                FileName = file.FileName,
                DownloadLimit = limit > 0 ? limit : null,
                ExpiresUtc = days > 0 ? now.AddDays(days) : null,
                CreatedUtc = now,
            }, collection: Collection, cancellationToken: cancellationToken);

            granted++;
        }

        return granted;
    }

    /// <summary>
    /// Takes back the files of one order line, such as when its order was canceled or refunded.
    /// </summary>
    /// <param name="orderId">The order's ID.</param>
    /// <param name="lineId">The line's ID.</param>
    /// <param name="reason">Why they are taken back.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many files were taken back.</returns>
    public async Task<int> RevokeLineAsync(string orderId, string lineId, string reason, CancellationToken cancellationToken = default)
    {
        var revoked = 0;

        foreach (var entitlement in await ListAsync(orderId, cancellationToken))
        {
            if (entitlement.RevokedUtc is null && string.Equals(entitlement.OrderLineId, lineId, StringComparison.Ordinal))
            {
                await RevokeAsync(entitlement, reason, cancellationToken);
                revoked++;
            }
        }

        return revoked;
    }

    /// <summary>
    /// Returns an order's entitlements, in the order they were given.
    /// </summary>
    public async Task<IReadOnlyList<DownloadEntitlement>> ListAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(orderId))
        {
            return [];
        }

        var entitlements = await _session
            .Query<DownloadEntitlement, DownloadEntitlementIndex>(index => index.OrderId == orderId, collection: Collection)
            .OrderBy(index => index.CreatedUtc)
            .ThenBy(index => index.Id)
            .ListAsync(cancellationToken);

        return [.. entitlements];
    }

    public Task<DownloadEntitlement> FindAsync(string itemId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(itemId)
            ? Task.FromResult<DownloadEntitlement>(null)
            : _session.Query<DownloadEntitlement, DownloadEntitlementIndex>(index => index.ItemId == itemId, collection: Collection).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Returns the link that downloads the entitlement's file.
    /// </summary>
    public Task<string> CreateUrlAsync(DownloadEntitlement entitlement)
    {
        ArgumentNullException.ThrowIfNull(entitlement);

        return _siteUrlGenerator.GetUrlAsync(DownloadsConstants.RouteNames.Download, new { token = _protector.Protect(entitlement.ItemId) });
    }

    /// <summary>
    /// Finds the entitlement a link names, and whether it still works.
    /// </summary>
    public async Task<(DownloadEntitlement Entitlement, DownloadProblem Problem)> ResolveAsync(string token, CancellationToken cancellationToken = default)
    {
        string itemId;

        try
        {
            itemId = string.IsNullOrWhiteSpace(token) ? null : _protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            itemId = null;
        }

        var entitlement = await FindAsync(itemId, cancellationToken);

        return (entitlement, GetProblem(entitlement, _clock.UtcNow));
    }

    /// <summary>
    /// Counts a download, unless the link stopped working meanwhile.
    /// </summary>
    /// <returns><see langword="false"/> when it no longer works.</returns>
    public async Task<bool> RecordDownloadAsync(DownloadEntitlement entitlement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entitlement);

        // Two downloads at once must not both take the last one.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync("DOWNLOAD_" + entitlement.ItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return false;
        }

        await using (locker)
        {
            var current = (await _session.GetAsync<DownloadEntitlement>([entitlement.Id], Collection, cancellationToken)).FirstOrDefault() ?? entitlement;

            if (GetProblem(current, _clock.UtcNow) != DownloadProblem.None)
            {
                return false;
            }

            current.DownloadCount++;
            current.LastDownloadedUtc = _clock.UtcNow;

            await _session.SaveAsync(current, collection: Collection, cancellationToken: cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return true;
        }
    }

    /// <summary>
    /// Takes back every file of an order, such as when it was canceled or refunded.
    /// </summary>
    public async Task<int> RevokeOrderAsync(string orderId, string reason, CancellationToken cancellationToken = default)
    {
        var revoked = 0;

        foreach (var entitlement in await ListAsync(orderId, cancellationToken))
        {
            if (entitlement.RevokedUtc is null)
            {
                await RevokeAsync(entitlement, reason, cancellationToken);
                revoked++;
            }
        }

        return revoked;
    }

    public async Task RevokeAsync(DownloadEntitlement entitlement, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entitlement);

        entitlement.RevokedUtc = _clock.UtcNow;
        entitlement.RevokeReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await _session.SaveAsync(entitlement, collection: Collection, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gives a buyer their downloads back: the count starts again, the link works for another lifetime, and a revoked
    /// right is restored.
    /// </summary>
    public async Task ResetAsync(DownloadEntitlement entitlement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entitlement);

        var settings = await _siteService.GetSettingsAsync<DownloadSettings>();
        var days = settings.LinkLifetimeDays;

        entitlement.DownloadCount = 0;
        entitlement.RevokedUtc = null;
        entitlement.RevokeReason = null;
        entitlement.ExpiresUtc = entitlement.ExpiresUtc.HasValue || days > 0 ? _clock.UtcNow.AddDays(days > 0 ? days : 30) : null;

        await _session.SaveAsync(entitlement, collection: Collection, cancellationToken: cancellationToken);
    }

    internal static DownloadProblem GetProblem(DownloadEntitlement entitlement, DateTime utcNow)
    {
        if (entitlement is null)
        {
            return DownloadProblem.NotFound;
        }

        if (entitlement.RevokedUtc.HasValue)
        {
            return DownloadProblem.Revoked;
        }

        if (entitlement.ExpiresUtc.HasValue && utcNow >= entitlement.ExpiresUtc.Value)
        {
            return DownloadProblem.Expired;
        }

        return entitlement.DownloadLimit.HasValue && entitlement.DownloadCount >= entitlement.DownloadLimit.Value
            ? DownloadProblem.LimitReached
            : DownloadProblem.None;
    }
}
