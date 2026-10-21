using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Downloads.Services;

/// <summary>
/// Puts the download links in the order's confirmation email.
/// </summary>
internal sealed class DownloadEmailSectionProvider : IOrderEmailSectionProvider
{
    private readonly DownloadService _downloads;

    internal readonly IStringLocalizer S;

    public DownloadEmailSectionProvider(DownloadService downloads, IStringLocalizer<DownloadEmailSectionProvider> stringLocalizer)
    {
        _downloads = downloads;
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
            if (DownloadService.GetProblem(entitlement, DateTime.UtcNow) != DownloadProblem.None || await _downloads.CreateUrlAsync(entitlement) is not { } url)
            {
                continue;
            }

            section.Links.Add(new OrderEmailLink(entitlement.FileName, url, Note(entitlement)));
        }

        return section;
    }

    private string Note(DownloadEntitlement entitlement)
    {
        if (entitlement.DownloadLimit.HasValue && entitlement.ExpiresUtc.HasValue)
        {
            return S["{0} downloads, until {1:d}", entitlement.DownloadLimit.Value, entitlement.ExpiresUtc.Value].Value;
        }

        if (entitlement.DownloadLimit.HasValue)
        {
            return S["{0} downloads", entitlement.DownloadLimit.Value].Value;
        }

        return entitlement.ExpiresUtc.HasValue ? S["until {0:d}", entitlement.ExpiresUtc.Value].Value : null;
    }
}
