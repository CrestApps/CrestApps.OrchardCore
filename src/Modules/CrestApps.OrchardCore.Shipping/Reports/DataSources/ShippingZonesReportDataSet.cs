using System.Runtime.CompilerServices;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Shipping.Reports.DataSources;

/// <summary>
/// One row per shipping zone, in the order zones are tried: the countries and regions it covers, whether it catches the
/// rest of the world, and how many methods it offers. Only a principal that may manage the shipping zones may read it.
/// </summary>
public sealed class ShippingZonesReportDataSet : RecordDataSet<ShippingZone>, IShippingReportDataSet
{
    private readonly INamedCatalog<ShippingZone> _zones;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShippingZonesReportDataSet"/> class.
    /// </summary>
    /// <param name="zones">The catalog of the shipping zones.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShippingZonesReportDataSet(
        INamedCatalog<ShippingZone> zones,
        IAuthorizationService authorizationService,
        IStringLocalizer<ShippingZonesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ShippingSource.ShippingZones,
                stringLocalizer["Shipping zones"],
                stringLocalizer["One row per shipping zone, in the order zones are tried: the countries and regions it covers and how many methods it offers."]))
    {
        _zones = zones;
        _authorizationService = authorizationService;

        var S = stringLocalizer;
        var zone = S["Zone"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Zone ID"], DataFieldType.Text, record => record.ItemId, zone).Identifier();
        AddField(nameof(ShippingZone.Name), S["Zone"], DataFieldType.Text, record => record.Name, zone);
        AddField(nameof(ShippingZone.Position), S["Position"], DataFieldType.Integer, record => record.Position, zone).Description = S["The zone's place in the order zones are tried, smallest first. The rest of the world is tried last."].Value;
        AddField(nameof(ShippingZone.Countries), S["Countries"], DataFieldType.Text, record => Join(record.Countries), zone).Description = S["The country codes the zone covers, separated by commas."].Value;
        AddField(nameof(ShippingZone.Regions), S["Regions"], DataFieldType.Text, record => Join(record.Regions), zone).Description = S["The region codes the zone is narrowed to, separated by commas. A country with no region listed is covered whole."].Value;
        AddField(nameof(ShippingZone.RestOfWorld), S["Rest of the world"], DataFieldType.Boolean, record => record.RestOfWorld, zone);
        AddField("MethodCount", S["Methods"], DataFieldType.Integer, record => record.Methods?.Count(method => method is not null) ?? 0, zone);
        AddField("EnabledMethodCount", S["Methods on"], DataFieldType.Integer, record => record.Methods?.Count(method => method?.Enabled == true) ?? 0, zone);

        AddField(nameof(ShippingZone.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(ShippingZone.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);

        Fields.MarkKeyFilterable([CommerceReportDataSets.ItemIdField]);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, ShippingPermissions.ManageShipping);
    }

    /// <summary>
    /// Puts zones in the order they are tried, as the zones' admin list shows them: by position, then by name, with the
    /// rest of the world last.
    /// </summary>
    /// <param name="zones">The zones.</param>
    /// <returns>The zones in order.</returns>
    internal static IEnumerable<ShippingZone> InOrder(IEnumerable<ShippingZone> zones)
    {
        return (zones ?? [])
            .Where(zone => zone is not null)
            .OrderBy(zone => zone.RestOfWorld)
            .ThenBy(zone => zone.Position)
            .ThenBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the keys a join asks for on a field, or <see langword="null"/> when it asks for none.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="field">The field.</param>
    /// <returns>The keys, or <see langword="null"/>.</returns>
    internal static HashSet<string> KeysOf(DataSourceQuery query, string field)
    {
        return DataConditions.Keys(query.Conditions, field)?
            .Select(key => key?.ToString())
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<ShippingZone> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var keys = KeysOf(query, CommerceReportDataSets.ItemIdField);

        if (keys is { Count: 0 })
        {
            yield break;
        }

        foreach (var zone in InOrder(await _zones.GetAllAsync(cancellationToken)))
        {
            if (keys is null || keys.Contains(zone.ItemId))
            {
                yield return zone;
            }
        }
    }

    private static string Join(IEnumerable<string> values)
    {
        var text = string.Join(", ", (values ?? []).Where(value => !string.IsNullOrEmpty(value)));

        return text.Length == 0 ? null : text;
    }
}
