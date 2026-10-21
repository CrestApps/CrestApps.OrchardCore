using CrestApps.OrchardCore.Classifieds.Indexes;
using CrestApps.OrchardCore.Classifieds.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Classifieds.Services;

/// <summary>
/// Listings people post: posted, then approved by a moderator (or at once), shown for their term, expired and renewed,
/// and featured or bumped by a paid upgrade.
/// </summary>
public sealed class ClassifiedService
{
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IUserService _userService;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public ClassifiedService(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<ClassifiedService> logger,
        IStringLocalizer<ClassifiedService> stringLocalizer,
        IUserService userService,
        ISiteService siteService)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        _userService = userService;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the content types visitors can post, with their settings.
    /// </summary>
    public async Task<IReadOnlyList<(string ContentType, string DisplayName, ClassifiedPartSettings Settings)>> ListTypesAsync()
        => [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Select(type => (Type: type, Part: type.Parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(ClassifiedPart))))
            .Where(entry => entry.Part is not null)
            .Select(entry => (entry.Type.Name, entry.Type.DisplayName, entry.Part.GetSettings<ClassifiedPartSettings>()))];

    public async Task<ClassifiedPartSettings> GetSettingsAsync(string contentType)
        => (await _contentDefinitionManager.GetTypeDefinitionAsync(contentType))?.Parts
            .FirstOrDefault(part => part.PartDefinition.Name == nameof(ClassifiedPart))?
            .GetSettings<ClassifiedPartSettings>();

    /// <summary>
    /// Takes a newly posted listing: it waits for a moderator, or is shown at once when its type needs none.
    /// </summary>
    public async Task PostAsync(ContentItem item)
    {
        var settings = await GetSettingsAsync(item.ContentType) ?? new ClassifiedPartSettings();

        item.Alter<ClassifiedPart>(part =>
        {
            part.Status = ClassifiedStatus.Pending;
            part.PostedUtc = _clock.UtcNow;
            part.ExpiresUtc = null;
            part.RejectReason = null;
        });

        await _contentManager.CreateAsync(item, VersionOptions.Draft);

        if (!settings.RequireApproval)
        {
            await ApproveAsync(item);
        }
    }

    /// <summary>
    /// Takes a poster's changes to their listing: it waits for a moderator again when its type needs one.
    /// </summary>
    public async Task ChangedAsync(ContentItem item)
    {
        var settings = await GetSettingsAsync(item.ContentType) ?? new ClassifiedPartSettings();

        if (settings.RequireApproval)
        {
            item.Alter<ClassifiedPart>(part => part.Status = ClassifiedStatus.Pending);
            await _contentManager.UpdateAsync(item);

            if (item.Published)
            {
                await _contentManager.UnpublishAsync(item);
            }

            return;
        }

        await _contentManager.UpdateAsync(item);

        if (item.TryGet<ClassifiedPart>(out var part) && part.Status == ClassifiedStatus.Approved)
        {
            await _contentManager.PublishAsync(item);
        }
    }

    /// <summary>
    /// Shows a listing for its term.
    /// </summary>
    public async Task ApproveAsync(ContentItem item)
    {
        var settings = await GetSettingsAsync(item.ContentType) ?? new ClassifiedPartSettings();

        item.Alter<ClassifiedPart>(part =>
        {
            part.Status = ClassifiedStatus.Approved;
            part.RejectReason = null;
            part.ExpiresUtc = _clock.UtcNow.AddDays(Math.Max(1, settings.TermDays));
        });

        await _contentManager.UpdateAsync(item);
        await _contentManager.PublishAsync(item);
    }

    public async Task RejectAsync(ContentItem item, string reason)
    {
        item.Alter<ClassifiedPart>(part =>
        {
            part.Status = ClassifiedStatus.Rejected;
            part.RejectReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        });

        await _contentManager.UpdateAsync(item);

        if (item.Published)
        {
            await _contentManager.UnpublishAsync(item);
        }

        await NotifyAsync(item, S["Your listing wasn't approved: {0}", item.DisplayText],
            string.IsNullOrWhiteSpace(reason) ? S["Your listing wasn't approved. You can change it and post it again."] : S["Your listing wasn't approved: {0}", reason.Trim()]);
    }

    /// <summary>
    /// Runs a listing for another term: from its end when it's still shown, or from now when it expired.
    /// </summary>
    public async Task<bool> RenewAsync(ContentItem item)
    {
        if (!item.TryGet<ClassifiedPart>(out var current) || current.Status is not (ClassifiedStatus.Approved or ClassifiedStatus.Expired))
        {
            return false;
        }

        var settings = await GetSettingsAsync(item.ContentType) ?? new ClassifiedPartSettings();
        var now = _clock.UtcNow;

        item.Alter<ClassifiedPart>(part =>
        {
            var from = part.Status == ClassifiedStatus.Approved && part.ExpiresUtc > now ? part.ExpiresUtc.Value : now;

            part.Status = ClassifiedStatus.Approved;
            part.ExpiresUtc = from.AddDays(Math.Max(1, settings.TermDays));
        });

        await _contentManager.UpdateAsync(item);
        await _contentManager.PublishAsync(item);

        return true;
    }

    /// <summary>
    /// Features or bumps a listing for an upgrade bought for it.
    /// </summary>
    public async Task<bool> UpgradeAsync(string contentItemId, ClassifiedUpgradePart upgrade, int units)
    {
        if (await _contentManager.GetAsync(contentItemId, VersionOptions.Latest) is not { } item || !item.Has<ClassifiedPart>())
        {
            return false;
        }

        var now = _clock.UtcNow;

        item.Alter<ClassifiedPart>(part =>
        {
            if (upgrade.Kind == ClassifiedUpgradeKind.Bump)
            {
                part.BumpedUtc = now;
            }
            else
            {
                var from = part.FeaturedUntilUtc > now ? part.FeaturedUntilUtc.Value : now;

                part.FeaturedUntilUtc = from.AddDays(Math.Max(1, upgrade.Days) * Math.Max(1, units));
            }
        });

        await _contentManager.UpdateAsync(item);

        if (item.TryGet<ClassifiedPart>(out var updated) && updated.Status == ClassifiedStatus.Approved)
        {
            await _contentManager.PublishAsync(item);
        }

        return true;
    }

    /// <summary>
    /// Takes down the listings whose term ran out, and tells their posters they can renew them.
    /// </summary>
    public async Task<int> ExpireDueAsync(int take, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var ids = (await _session.QueryIndex<ClassifiedIndex>(index => index.Latest && index.Status == ClassifiedStatus.Approved && index.ExpiresUtc != null && index.ExpiresUtc < now)
            .Take(take)
            .ListAsync(cancellationToken)).Select(index => index.ContentItemId).Distinct().ToList();

        foreach (var id in ids)
        {
            if (await _contentManager.GetAsync(id, VersionOptions.Latest) is not { } item)
            {
                continue;
            }

            item.Alter<ClassifiedPart>(part => part.Status = ClassifiedStatus.Expired);
            await _contentManager.UpdateAsync(item);

            if (item.Published)
            {
                await _contentManager.UnpublishAsync(item);
            }

            await NotifyAsync(item, S["Your listing has expired: {0}", item.DisplayText], S["Your listing {0} has run its term and isn't shown any more. Renew it from My listings in your account.", item.DisplayText]);
        }

        return ids.Count;
    }

    /// <summary>
    /// Returns a poster's listings, the latest version of each, newest first.
    /// </summary>
    public async Task<IReadOnlyList<ContentItem>> ListOwnAsync(string owner, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(owner)
            ? []
            : [.. await _session.Query<ContentItem, ClassifiedIndex>(index => index.Owner == owner && index.Latest).OrderByDescending(index => index.SortUtc).Take(200).ListAsync(cancellationToken)];

    /// <summary>
    /// Returns listings with a status, the latest version of each, for moderators.
    /// </summary>
    public async Task<(int Count, IReadOnlyList<ContentItem> Items)> PageAsync(ClassifiedStatus? status, string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _session.Query<ContentItem, ClassifiedIndex>(index => index.Latest);

        if (status is { } wanted)
        {
            query = query.Where(index => index.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var start = search.Trim();

            query = query.Where(index => index.DisplayText.StartsWith(start));
        }

        var count = await query.CountAsync(cancellationToken);

        return (count, [.. await query.OrderByDescending(index => index.SortUtc).Skip(skip).Take(take).ListAsync(cancellationToken)]);
    }

    /// <summary>
    /// Finds shown listings: of a type, near a place when one is given, featured ones first, then the newest.
    /// </summary>
    public async Task<IReadOnlyList<(ContentItem Item, double? Kilometers)>> SearchAsync(string contentType, string text, double? latitude, double? longitude, double kilometers, int take, CancellationToken cancellationToken = default)
    {
        var query = _session.Query<ContentItem, ClassifiedIndex>(index => index.Published && index.Status == ClassifiedStatus.Approved);

        if (!string.IsNullOrEmpty(contentType))
        {
            query = query.Where(index => index.ContentType == contentType);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var start = text.Trim();

            query = query.Where(index => index.DisplayText.Contains(start));
        }

        var near = GeoDistance.IsValid(latitude, longitude);

        if (near)
        {
            var (minLatitude, maxLatitude, minLongitude, maxLongitude) = GeoDistance.GetBounds(latitude.Value, longitude.Value, kilometers);

            query = query.Where(index => index.Latitude >= minLatitude && index.Latitude <= maxLatitude && index.Longitude >= minLongitude && index.Longitude <= maxLongitude);
        }

        var now = _clock.UtcNow;
        var items = await query.OrderByDescending(index => index.SortUtc).Take(near ? 1000 : take * 2).ListAsync(cancellationToken);

        return [.. items
            .Select(item =>
            {
                item.TryGet<ClassifiedPart>(out var part);

                var distance = near && GeoDistance.IsValid(part?.Latitude, part?.Longitude)
                    ? GeoDistance.GetKilometers(latitude.Value, longitude.Value, part.Latitude.Value, part.Longitude.Value)
                    : (double?)null;

                return (Item: item, Part: part, Kilometers: distance);
            })
            .Where(entry => !near || entry.Kilometers <= kilometers)
            .OrderByDescending(entry => entry.Part?.FeaturedUntilUtc > now)
            .ThenBy(entry => near ? entry.Kilometers : 0d)
            .ThenByDescending(entry => entry.Part?.BumpedUtc ?? entry.Part?.PostedUtc ?? entry.Item.CreatedUtc)
            .Take(take)
            .Select(entry => (entry.Item, entry.Kilometers))];
    }

    private async Task NotifyAsync(ContentItem item, string subject, string body)
    {
        if (string.IsNullOrEmpty(item.Owner) ||
            _serviceProvider.GetService<IEmailService>() is not { } email ||
            await _userService.GetUserByUniqueIdAsync(item.Owner) is not User { Email.Length: > 0 } user)
        {
            return;
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var result = await email.SendAsync(new MailMessage
        {
            To = user.Email,
            Subject = subject,
            TextBody = S["Hello {0},", user.UserName] + Environment.NewLine + Environment.NewLine + body + Environment.NewLine + Environment.NewLine + site.SiteName,
        });

        if (!result.Succeeded)
        {
            _logger.LogWarning("The email about listing '{ContentItemId}' couldn't be sent.", item.ContentItemId);
        }
    }
}
