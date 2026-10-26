using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Inventory.Reports.DataSources;

/// <summary>
/// One row per place stock is kept, from the Inventory settings, in the order orders take units from them. The default
/// location is always one of them, even when it was never named. Each row is a location of the list the settings hold,
/// with its place in that list.
/// </summary>
public sealed class LocationsReportDataSet : RecordDataSet<ReportChildRow<IReadOnlyList<InventoryLocation>, InventoryLocation>>, IInventoryReportDataSet
{
    private readonly IInventoryLocationService _locationService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocationsReportDataSet"/> class.
    /// </summary>
    /// <param name="locationService">The service that lists the locations.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LocationsReportDataSet(
        IInventoryLocationService locationService,
        IAuthorizationService authorizationService,
        IStringLocalizer<LocationsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InventorySource.Locations,
                stringLocalizer["Locations"],
                stringLocalizer["One row per place stock is kept, in the order orders take units from them."]))
    {
        _locationService = locationService;
        _authorizationService = authorizationService;

        var S = stringLocalizer;
        var location = S["Location"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Location ID"], DataFieldType.Text, row => row.Child.LocationId, location).Identifier();
        AddField(nameof(InventoryLocation.Name), S["Location"], DataFieldType.Text, row => row.Child.Name, location);
        AddField(nameof(InventoryLocation.Address), S["Address"], DataFieldType.Text, row => row.Child.Address, location);
        AddField("Position", S["Order taken from"], DataFieldType.Integer, row => row.Position, location).Description = S["The place of the location in the order orders take units from, from 1."].Value;
        AddField("IsDefault", S["Default location"], DataFieldType.Boolean, row => row.Child.LocationId == InventoryConstants.DefaultLocationId, location).Description = S["Stock counted before there were locations is kept here."].Value;

        Fields.MarkKeyFilterable([CommerceReportDataSets.ItemIdField]);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, InventoryPermissions.ViewInventory);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<ReportChildRow<IReadOnlyList<InventoryLocation>, InventoryLocation>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var keys = DataConditions.Keys(query.Conditions, CommerceReportDataSets.ItemIdField);

        if (keys is { Count: 0 })
        {
            yield break;
        }

        var wanted = keys?.Select(key => key?.ToString()).ToHashSet(StringComparer.Ordinal);
        var locations = await _locationService.GetLocationsAsync();

        foreach (var row in ReportChildRow<IReadOnlyList<InventoryLocation>, InventoryLocation>.For(locations, locations))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (wanted is null || wanted.Contains(row.Child.LocationId))
            {
                yield return row;
            }
        }
    }
}
