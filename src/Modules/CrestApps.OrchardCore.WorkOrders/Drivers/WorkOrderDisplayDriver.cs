using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.WorkOrders.Controllers;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.WorkOrders.Drivers;

/// <summary>
/// The main section of the work order editor: details, who and where, when, the assets and the planned lines. Other
/// modules add their own sections to the editor with display drivers of <see cref="WorkOrder"/>.
/// </summary>
internal sealed class WorkOrderDisplayDriver : DisplayDriver<WorkOrder>
{
    private const int BlankLines = 2;

    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderLookups _lookups;
    private readonly WorkOrderCustomers _customers;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;

    public WorkOrderDisplayDriver(
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderLookups lookups,
        WorkOrderCustomers customers,
        ILocalClock localClock,
        IClock clock,
        IServiceProvider serviceProvider)
    {
        _types = types;
        _lookups = lookups;
        _customers = customers;
        _localClock = localClock;
        _clock = clock;
        _serviceProvider = serviceProvider;
    }

    public override IDisplayResult Edit(WorkOrder workOrder, BuildEditorContext context)
    {
        return Initialize<WorkOrderEditViewModel>("WorkOrderFields_Edit", async model =>
        {
            var address = workOrder.ServiceAddress;

            model.WorkOrderId = string.IsNullOrEmpty(workOrder.Number) ? null : workOrder.ItemId;
            model.TypeId = workOrder.TypeId;
            model.TypeName = string.IsNullOrEmpty(workOrder.TypeId) ? null : (await _types.FindByIdAsync(workOrder.TypeId))?.Name;
            model.Number = workOrder.Number;
            model.Summary = workOrder.Summary;
            model.Description = workOrder.Description;
            model.Priority = workOrder.Priority;
            model.EstimatedMinutes = workOrder.EstimatedMinutes;
            model.ContactId = workOrder.ContactId;
            model.CustomerId = workOrder.CustomerId;
            model.AccountId = workOrder.AccountId;
            model.ContactName = workOrder.Contact?.Name;
            model.ContactPhone = workOrder.Contact?.Phone;
            model.ContactEmail = workOrder.Contact?.Email;
            model.AddressLine1 = address?.AddressLine1;
            model.AddressLine2 = address?.AddressLine2;
            model.City = address?.City;
            model.Region = address?.Region;
            model.PostalCode = address?.PostalCode;
            model.Country = address?.Country;
            model.Latitude = address?.Latitude;
            model.Longitude = address?.Longitude;
            model.AccessNotes = workOrder.AccessNotes;
            model.Earliest = workOrder.EarliestUtc is { } earliest ? (await _localClock.ConvertToLocalAsync(earliest)).DateTime : null;
            model.Latest = workOrder.LatestUtc is { } latest ? (await _localClock.ConvertToLocalAsync(latest)).DateTime : null;
            model.AssetIds = [.. workOrder.AssetIds];
            model.Lines = [.. workOrder.Lines.Select(WorkOrderSettingsController.ToEditModel)];

            model.AssetOptions = [.. (await _lookups.AssetsAsync()).Select(option => new SelectListItem(option.Text, option.Value, model.AssetIds.Contains(option.Value)))];
            model.StockItemOptions = await _lookups.StockItemsAsync();
            model.CustomerPickers = await _customers.NamesAsync(model.CustomerId, model.AccountId, model.ContactId, pickers: true);
            model.Map = _serviceProvider.GetService<IGeoSettingsService>() is { } geo ? await geo.GetAsync() : null;

            while (model.Lines.Count(line => string.IsNullOrWhiteSpace(line.Description)) < BlankLines)
            {
                model.Lines.Add(new LineEditModel());
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(WorkOrder workOrder, UpdateEditorContext context)
    {
        var model = new WorkOrderEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        workOrder.Summary = model.Summary?.Trim();
        workOrder.Description = Clean(model.Description);
        workOrder.Priority = model.Priority;
        workOrder.EstimatedMinutes = Math.Max(0, model.EstimatedMinutes);
        workOrder.ContactId = Clean(model.ContactId);
        workOrder.CustomerId = Clean(model.CustomerId);
        workOrder.AccountId = Clean(model.AccountId);

        // A contact picked with no name typed gives the work order its name, and its account when none is chosen.
        if (string.IsNullOrWhiteSpace(model.ContactName) && !string.IsNullOrEmpty(workOrder.ContactId))
        {
            model.ContactName = await _customers.NameAsync(AssetOwnerKind.Contact, workOrder.ContactId);
        }

        workOrder.AccountId ??= await _customers.AccountOfAsync(workOrder.ContactId, workOrder.CustomerId);

        workOrder.Contact = new WorkOrderContact { Name = Clean(model.ContactName), Phone = Clean(model.ContactPhone), Email = Clean(model.ContactEmail) };
        workOrder.AccessNotes = Clean(model.AccessNotes);
        workOrder.EarliestUtc = model.Earliest is { } earliest ? await _localClock.ConvertToUtcAsync(earliest) : null;
        workOrder.LatestUtc = model.Latest is { } latest ? await _localClock.ConvertToUtcAsync(latest) : null;
        workOrder.AssetIds = [.. (model.AssetIds ?? []).Where(assetId => !string.IsNullOrEmpty(assetId)).Distinct(StringComparer.Ordinal)];

        var previous = workOrder.Lines.Where(line => !string.IsNullOrEmpty(line.Id)).ToDictionary(line => line.Id, StringComparer.Ordinal);

        workOrder.Lines = [.. (model.Lines ?? []).Where(line => !string.IsNullOrWhiteSpace(line.Description)).Select(line => WorkOrderSettingsController.ToLine(line, string.IsNullOrEmpty(line.Id) ? null : previous.GetValueOrDefault(line.Id)))];

        // Lines that already used parts stay, even when their row was cleared: what was used is a record.
        foreach (var used in previous.Values.Where(line => line.QuantityUsed > 0 && !workOrder.Lines.Any(item => item.Id == line.Id)))
        {
            workOrder.Lines.Add(used);
        }

        ApplyAddress(model, workOrder);

        // With no address typed, the job is at the account's location.
        workOrder.ServiceAddress ??= await _customers.AccountAddressAsync(workOrder.AccountId);

        return Edit(workOrder, context);
    }

    private void ApplyAddress(WorkOrderEditViewModel model, WorkOrder workOrder)
    {
        var address = new Address
        {
            AddressLine1 = Clean(model.AddressLine1),
            AddressLine2 = Clean(model.AddressLine2),
            City = Clean(model.City),
            Region = Clean(model.Region),
            PostalCode = Clean(model.PostalCode),
            Country = Clean(model.Country),
        };
        var current = workOrder.ServiceAddress;
        var pinned = GeoPointOf(model.Latitude, model.Longitude);

        if (address.AddressLine1 is null && address.City is null && address.PostalCode is null)
        {
            workOrder.ServiceAddress = null;
        }
        else if (pinned is { } pin && (pin.Latitude != current?.Latitude || pin.Longitude != current?.Longitude))
        {
            // Placed by hand on the map: the pin is where the job is, whatever geocoding would say.
            workOrder.ServiceAddress = new Address
            {
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                City = address.City,
                Region = address.Region,
                PostalCode = address.PostalCode,
                Country = address.Country,
                Latitude = pin.Latitude,
                Longitude = pin.Longitude,
                GeocodedUtc = _clock.UtcNow,
                GeocodeSource = "Pin",
            };

            // The territory is found again from the pin.
            workOrder.TerritoryId = null;
        }
        else if (current is null
            || current.AddressLine1 != address.AddressLine1
            || current.AddressLine2 != address.AddressLine2
            || current.City != address.City
            || current.Region != address.Region
            || current.PostalCode != address.PostalCode
            || current.Country != address.Country)
        {
            // A new address has no point until it is geocoded.
            workOrder.ServiceAddress = address;
        }
    }

    private static (double Latitude, double Longitude)? GeoPointOf(double? latitude, double? longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 ? (Math.Round(latitude.Value, 6), Math.Round(longitude.Value, 6)) : null;

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
