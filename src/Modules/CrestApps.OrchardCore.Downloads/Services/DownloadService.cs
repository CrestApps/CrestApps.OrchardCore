using System.Security.Cryptography;
using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
    private readonly IContentManager _contentManager;
    private readonly ISiteService _siteService;
    private readonly IDataProtector _protector;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    public DownloadService(
        ISession session,
        IContentManager contentManager,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _session = session;
        _contentManager = contentManager;
        _siteService = siteService;
        _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose);
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <summary>
    /// Gives a paid order's buyer the files of the digital products it bought, the product's files as they were when
    /// it was ordered. Granting again adds only what is missing.
    /// </summary>
    /// <returns>How many files were given.</returns>
    public async Task<int> GrantAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var settings = await _siteService.GetSettingsAsync<DownloadSettings>();
        var existing = await ListAsync(order.ItemId, cancellationToken);
        var now = _clock.UtcNow;
        var granted = 0;

        foreach (var line in order.Lines ?? [])
        {
            var product = !string.IsNullOrEmpty(line.ContentItemVersionId)
                ? await _contentManager.GetVersionAsync(line.ContentItemVersionId)
                : await _contentManager.GetAsync(line.ContentItemId, VersionOptions.Published);

            if (product?.TryGet<DigitalFilesPart>(out var part) != true || part.Files is not { Count: > 0 })
            {
                continue;
            }

            var limit = part.DownloadLimit ?? settings.DownloadLimit;
            var days = part.LinkLifetimeDays ?? settings.LinkLifetimeDays;

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
        }

        return granted;
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
    public async Task<string> CreateUrlAsync(DownloadEntitlement entitlement)
    {
        ArgumentNullException.ThrowIfNull(entitlement);

        var token = _protector.Protect(entitlement.ItemId);
        var site = await _siteService.GetSiteSettingsAsync();

        // An email is often sent where there is no request, so the site's own address comes first.
        if (!string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return $"{site.BaseUrl.TrimEnd('/')}/downloads/{Uri.EscapeDataString(token)}";
        }

        var httpContext = _httpContextAccessor.HttpContext;

        return httpContext is null
            ? null
            : _linkGenerator.GetUriByRouteValues(httpContext, DownloadsConstants.RouteNames.Download, new { token });
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
