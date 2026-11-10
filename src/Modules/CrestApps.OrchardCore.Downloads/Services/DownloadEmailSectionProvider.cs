using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Downloads.Services;

/// <summary>
/// Puts the download links in the order's confirmation email.
/// </summary>
internal sealed class DownloadEmailSectionProvider : IOrderEmailSectionProvider
{
    private readonly DownloadService _downloads;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public DownloadEmailSectionProvider(
        DownloadService downloads,
        IStringLocalizer<DownloadEmailSectionProvider> stringLocalizer,
        IClock clock,
        ILocalClock localClock)
    {
        _downloads = downloads;
        _clock = clock;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public async Task<OrderEmailSection> GetSectionAsync(Order order, CancellationToken cancellationToken = default)
    {
        var section = new OrderEmailSection
        {
            Title = S["Your downloads"].Value,
            Intro = S["Download your files from these links. Keep this email: the links are yours."].Value,
        };

        foreach (var entitlement in await _downloads.ListAsync(order.ItemId, cancellationToken))
        {
            if (DownloadService.GetProblem(entitlement, _clock.UtcNow) != DownloadProblem.None || await _downloads.CreateUrlAsync(entitlement) is not { } url)
            {
                continue;
            }

            section.Links.Add(new OrderEmailLink(entitlement.FileName, url, await NoteAsync(entitlement)));
        }

        return section;
    }

    private async Task<string> NoteAsync(DownloadEntitlement entitlement)
    {
        // The last day the link works, in the site's time zone.
        var expires = entitlement.ExpiresUtc.HasValue
            ? (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(entitlement.ExpiresUtc.Value, DateTimeKind.Utc))).DateTime
            : (DateTime?)null;

        if (entitlement.DownloadLimit.HasValue && expires.HasValue)
        {
            return S["{0} downloads, until {1:d}", entitlement.DownloadLimit.Value, expires.Value].Value;
        }

        if (entitlement.DownloadLimit.HasValue)
        {
            return S["{0} downloads", entitlement.DownloadLimit.Value].Value;
        }

        return expires.HasValue ? S["until {0:d}", expires.Value].Value : null;
    }
}
