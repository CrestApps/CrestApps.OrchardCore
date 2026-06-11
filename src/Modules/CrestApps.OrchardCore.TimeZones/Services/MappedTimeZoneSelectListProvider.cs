using Microsoft.AspNetCore.Mvc.Rendering;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TimeZones.Services;

/// <summary>
/// Provides mapped time zones as display-ready select list items.
/// </summary>
public sealed class MappedTimeZoneSelectListProvider : ITimeZoneSelectListProvider
{
    private readonly INamedCatalog<TimeZoneMap> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="MappedTimeZoneSelectListProvider"/> class.
    /// </summary>
    /// <param name="catalog">The time zone map catalog.</param>
    public MappedTimeZoneSelectListProvider(INamedCatalog<TimeZoneMap> catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SelectListItem>> GetTimeZoneSelectListItemsAsync()
    {
        var maps = await _catalog.GetAllAsync().AsTask();

        return maps
            .OrderBy(map => map.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(map => map.TimeZoneId, StringComparer.OrdinalIgnoreCase)
            .Select(map => new SelectListItem(map.Name, map.TimeZoneId))
            .ToArray();
    }

    public async ValueTask<IEnumerable<KeyValuePair<string, string>>> GetTimeZoneSelectListAsync(CancellationToken cancellationToken = default)
    {
        var items = await GetTimeZoneSelectListItemsAsync();

        return items.Select(item => new KeyValuePair<string, string>(item.Value, item.Text));
    }
}
