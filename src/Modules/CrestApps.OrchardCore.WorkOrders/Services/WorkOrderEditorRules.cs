using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// The rules of the main section of the work order editor: how a work order fills its fields, and how the posted
/// fields are applied to it before <c>WorkOrderService</c> checks and saves it. The editor's display driver and the AI
/// tools both use them, so a work order changed either way gets the same contact, account, address and lines.
/// </summary>
internal static class WorkOrderEditorRules
{
    /// <summary>
    /// Fills the editor's fields, not its options, from a work order, with its times in the local time zone.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="model">The fields to fill.</param>
    /// <param name="localClock">The local clock.</param>
    /// <returns>A task that completes when the fields are filled.</returns>
    public static async Task FillAsync(WorkOrder workOrder, WorkOrderEditViewModel model, ILocalClock localClock)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(localClock);

        var address = workOrder.ServiceAddress;

        model.WorkOrderId = string.IsNullOrEmpty(workOrder.Number) ? null : workOrder.ItemId;
        model.TypeId = workOrder.TypeId;
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
        model.Earliest = workOrder.EarliestUtc is { } earliest ? (await localClock.ConvertToLocalAsync(earliest)).DateTime : null;
        model.Latest = workOrder.LatestUtc is { } latest ? (await localClock.ConvertToLocalAsync(latest)).DateTime : null;
        model.AssetIds = [.. workOrder.AssetIds];
        model.Lines = [.. workOrder.Lines.Select(WorkOrderSettingsEditorRules.ToEditModel)];
    }

    /// <summary>
    /// Applies the editor's fields to a work order: a contact picked with no name typed gives its name, a work order
    /// with no account takes its contact's or customer's, lines that already used parts are kept, and with no address
    /// typed the job is at the account's location.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="model">The fields.</param>
    /// <param name="customers">The customers, accounts and contacts.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the fields are applied.</returns>
    public static async Task ApplyAsync(WorkOrder workOrder, WorkOrderEditViewModel model, WorkOrderCustomers customers, ILocalClock localClock, IClock clock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(customers);
        ArgumentNullException.ThrowIfNull(localClock);
        ArgumentNullException.ThrowIfNull(clock);

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
            model.ContactName = await customers.NameAsync(AssetOwnerKind.Contact, workOrder.ContactId, cancellationToken);
        }

        workOrder.AccountId ??= await customers.AccountOfAsync(workOrder.ContactId, workOrder.CustomerId, cancellationToken);

        workOrder.Contact = new WorkOrderContact { Name = Clean(model.ContactName), Phone = Clean(model.ContactPhone), Email = Clean(model.ContactEmail) };
        workOrder.AccessNotes = Clean(model.AccessNotes);
        workOrder.EarliestUtc = model.Earliest is { } earliest ? await localClock.ConvertToUtcAsync(earliest) : null;
        workOrder.LatestUtc = model.Latest is { } latest ? await localClock.ConvertToUtcAsync(latest) : null;
        workOrder.AssetIds = [.. (model.AssetIds ?? []).Where(assetId => !string.IsNullOrEmpty(assetId)).Distinct(StringComparer.Ordinal)];

        var previous = workOrder.Lines.Where(line => !string.IsNullOrEmpty(line.Id)).ToDictionary(line => line.Id, StringComparer.Ordinal);

        workOrder.Lines = [.. (model.Lines ?? []).Where(line => !string.IsNullOrWhiteSpace(line.Description)).Select(line => WorkOrderSettingsEditorRules.ToLine(line, string.IsNullOrEmpty(line.Id) ? null : previous.GetValueOrDefault(line.Id)))];

        // Lines that already used parts stay, even when their row was cleared: what was used is a record.
        foreach (var used in previous.Values.Where(line => line.QuantityUsed > 0 && !workOrder.Lines.Any(item => item.Id == line.Id)))
        {
            workOrder.Lines.Add(used);
        }

        ApplyAddress(model, workOrder, clock);

        // With no address typed, the job is at the account's location.
        workOrder.ServiceAddress ??= await customers.AccountAddressAsync(workOrder.AccountId, cancellationToken);
    }

    private static void ApplyAddress(WorkOrderEditViewModel model, WorkOrder workOrder, IClock clock)
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
                GeocodedUtc = clock.UtcNow,
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
