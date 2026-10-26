using System.Runtime.CompilerServices;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Shipping.Reports.DataSources;

/// <summary>
/// One row per shipping method a zone offers, in the order zones are tried and, within a zone, in the zone's order: its
/// name, the rate provider that prices it, whether it is on, its currency and, for flat and free methods, the amounts
/// they charge or start from. Only a principal that may manage the shipping zones may read it.
/// </summary>
public sealed class ShippingMethodsReportDataSet : RecordDataSet<ReportChildRow<ShippingZone, ShippingMethod>>, IShippingReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the zone a method belongs to.
    /// </summary>
    public const string ZoneIdField = "ZoneId";

    private readonly INamedCatalog<ShippingZone> _zones;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShippingMethodsReportDataSet"/> class.
    /// </summary>
    /// <param name="zones">The catalog of the shipping zones.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShippingMethodsReportDataSet(
        INamedCatalog<ShippingZone> zones,
        IAuthorizationService authorizationService,
        IStringLocalizer<ShippingMethodsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ShippingSource.ShippingMethods,
                stringLocalizer["Shipping methods"],
                stringLocalizer["One row per shipping method a zone offers: its rate provider, whether it is on, its currency and the amounts of flat and free methods."]))
    {
        _zones = zones;
        _authorizationService = authorizationService;

        var S = stringLocalizer;
        var zone = S["Zone"].Value;
        var method = S["Method"].Value;
        var rate = S["Rate"].Value;

        AddField(ZoneIdField, S["Zone ID"], DataFieldType.Text, row => row.Parent.ItemId, zone)
            .Identifier(ShippingReportDataSource.Own(CommerceReportDataSets.ShippingSource.ShippingZones));
        AddField("ZoneName", S["Zone"], DataFieldType.Text, row => row.Parent.Name, zone);

        AddField(nameof(ShippingMethod.MethodId), S["Method ID"], DataFieldType.Text, row => row.Child.MethodId, method).Identifier();
        AddField(nameof(ShippingMethod.Name), S["Method"], DataFieldType.Text, row => row.Child.Name, method);
        AddField(nameof(ShippingMethod.Description), S["Description"], DataFieldType.Text, row => row.Child.Description, method).Description = S["What buyers see under the method's name."].Value;
        AddField(nameof(ShippingMethod.ProviderName), S["Rate provider"], DataFieldType.Text, row => row.Child.ProviderName, method);
        AddField(nameof(ShippingMethod.Enabled), S["On"], DataFieldType.Boolean, row => row.Child.Enabled, method);
        AddField(nameof(ShippingMethod.Position), S["Position"], DataFieldType.Integer, row => row.Child.Position, method).Description = S["The method's place in the zone's list, smallest first."].Value;
        AddField(nameof(ShippingMethod.TaxCategoryCode), S["Tax category"], DataFieldType.Text, row => row.Child.TaxCategoryCode, method);

        AddField(nameof(ShippingMethod.Currency), S["Currency"], DataFieldType.Text, row => row.Child.Currency, rate);
        AddField("FlatAmount", S["Flat amount"], DataFieldType.Decimal, row => Flat(row.Child)?.Amount, rate).Description = S["What a flat method charges once per order, in the method's currency. Empty for other methods."].Value;
        AddField("PerItemAmount", S["Amount per item"], DataFieldType.Decimal, row => Flat(row.Child)?.PerItemAmount, rate).Description = S["What a flat method adds for each item shipped, in the method's currency. Empty for other methods."].Value;
        AddField("FreeFromSubtotal", S["Free from subtotal"], DataFieldType.Decimal, row => Free(row.Child)?.MinimumSubtotal, rate).Description = S["The order subtotal from which a free method applies, in the method's currency; zero for always. Empty for other methods."].Value;

        Fields.MarkKeyFilterable([ZoneIdField, nameof(ShippingMethod.MethodId)]);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, ShippingPermissions.ManageShipping);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<ReportChildRow<ShippingZone, ShippingMethod>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var zoneIds = ShippingZonesReportDataSet.KeysOf(query, ZoneIdField);
        var methodIds = ShippingZonesReportDataSet.KeysOf(query, nameof(ShippingMethod.MethodId));

        if (zoneIds is { Count: 0 } || methodIds is { Count: 0 })
        {
            yield break;
        }

        foreach (var zone in ShippingZonesReportDataSet.InOrder(await _zones.GetAllAsync(cancellationToken)))
        {
            if (zoneIds is not null && !zoneIds.Contains(zone.ItemId))
            {
                continue;
            }

            var methods = (zone.Methods ?? [])
                .Where(method => method is not null)
                .OrderBy(method => method.Position)
                .ThenBy(method => method.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var row in ReportChildRow<ShippingZone, ShippingMethod>.For(zone, methods))
            {
                if (methodIds is null || methodIds.Contains(row.Child.MethodId))
                {
                    yield return row;
                }
            }
        }
    }

    // A method of the provider without settings yet is priced with the settings' defaults, as the provider does.
    private static FlatRateSettings Flat(ShippingMethod method)
    {
        if (!string.Equals(method.ProviderName, ShippingConstants.Providers.Flat, StringComparison.Ordinal))
        {
            return null;
        }

        return method.TryGet<FlatRateSettings>(out var settings)
            ? settings
            : new FlatRateSettings();
    }

    private static FreeShippingSettings Free(ShippingMethod method)
    {
        if (!string.Equals(method.ProviderName, ShippingConstants.Providers.Free, StringComparison.Ordinal))
        {
            return null;
        }

        return method.TryGet<FreeShippingSettings>(out var settings)
            ? settings
            : new FreeShippingSettings();
    }
}
