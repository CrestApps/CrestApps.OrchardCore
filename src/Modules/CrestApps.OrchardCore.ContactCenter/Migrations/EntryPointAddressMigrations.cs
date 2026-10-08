using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Moves every place a number used to be routed from onto the inbound entry points, so each number is routed from one
/// place: the entry point chosen on a phone number's own screen, the numbers typed on entry points, and the number a
/// queue named as its own.
/// </summary>
/// <remarks>
/// The explicit choice on a phone number wins over a typed number, and an earlier entry point over a later one; a number
/// already answered on a channel is not given to a second entry point, which is logged. A typed number with no address
/// becomes one, used for calls. A queue's number becomes an entry point that routes to that queue, unless an entry
/// point already answers it.
/// </remarks>
internal sealed class EntryPointAddressMigrations : DataMigration
{
    private const string PhoneRoutingSettingsKey = "PhoneEndpointRoutingSettings";

    private readonly IContactCenterEntryPointManager _entryPointManager;
    private readonly IOmnichannelChannelEndpointManager _addressManager;
    private readonly IEnumerable<IActivityQueueManager> _queueManagers;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntryPointAddressMigrations"/> class.
    /// </summary>
    /// <param name="entryPointManager">The entry points.</param>
    /// <param name="addressManager">The address list.</param>
    /// <param name="queueManagers">The queues, which may name a number of their own.</param>
    /// <param name="phoneNumberService">The phone number service, to compare numbers in one form.</param>
    /// <param name="logger">The logger.</param>
    public EntryPointAddressMigrations(
        IContactCenterEntryPointManager entryPointManager,
        IOmnichannelChannelEndpointManager addressManager,
        IEnumerable<IActivityQueueManager> queueManagers,
        IPhoneNumberService phoneNumberService,
        ILogger<EntryPointAddressMigrations> logger)
    {
        _entryPointManager = entryPointManager;
        _addressManager = addressManager;
        _queueManagers = queueManagers;
        _phoneNumberService = phoneNumberService;
        _logger = logger;
    }

    /// <summary>
    /// Moves the numbers onto the entry points.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        var entryPoints = (await _entryPointManager.GetAllAsync()).ToList();
        var addresses = (await _addressManager.GetAllAsync()).ToList();
        var changedEntryPoints = new HashSet<string>(StringComparer.Ordinal);
        var moved = 0;
        var created = 0;
        var skipped = 0;

        foreach (var entryPoint in entryPoints)
        {
            if (string.IsNullOrWhiteSpace(entryPoint.Channel))
            {
                entryPoint.Channel = OmnichannelConstants.Channels.Phone;
                changedEntryPoints.Add(entryPoint.ItemId);
            }

            entryPoint.AddressIds ??= [];
        }

        // 1. The entry point a phone number named on its own screen.
        foreach (var address in addresses.Where(address => address.Properties?.ContainsKey(PhoneRoutingSettingsKey) == true))
        {
            var entryPointId = ReadEntryPointId(address.Properties[PhoneRoutingSettingsKey]);
            var chosen = entryPoints.FirstOrDefault(entryPoint => entryPoint.ItemId == entryPointId);

            if (chosen is not null)
            {
                // The number's own choice wins, so no other call entry point keeps it.
                foreach (var other in entryPoints.Where(other => other != chosen && IsCallEntryPoint(other)))
                {
                    if (other.AddressIds.Remove(address.ItemId))
                    {
                        changedEntryPoints.Add(other.ItemId);
                    }
                }

                if (!chosen.AddressIds.Contains(address.ItemId))
                {
                    chosen.AddressIds.Add(address.ItemId);
                    changedEntryPoints.Add(chosen.ItemId);
                    moved++;
                }
            }

            address.Properties.Remove(PhoneRoutingSettingsKey);
            await _addressManager.UpdateAsync(address);
        }

        // 2. The numbers typed on entry points.
        foreach (var entryPoint in entryPoints.Where(entryPoint => entryPoint.DialedNumbers is { Count: > 0 }))
        {
            foreach (var number in entryPoint.DialedNumbers.Where(number => !string.IsNullOrWhiteSpace(number)))
            {
                var value = Canonicalize(number);
                var address = addresses.FirstOrDefault(candidate =>
                    candidate.GetAddressType() == OmnichannelAddressTypes.PhoneNumber &&
                    string.Equals(candidate.Value, value, StringComparison.OrdinalIgnoreCase));

                if (address is null)
                {
                    address = await _addressManager.NewAsync();
                    address.DisplayText = value;
                    address.AddressType = OmnichannelAddressTypes.PhoneNumber;
                    address.Capabilities = [OmnichannelConstants.Channels.Phone];
                    address.Value = value;
                    address.CreatedUtc = DateTime.UtcNow;

                    await _addressManager.CreateAsync(address);
                    addresses.Add(address);
                    created++;
                }
                else if (!address.HasCapability(OmnichannelConstants.Channels.Phone))
                {
                    // An entry point answered calls to it, so the number is used for calls.
                    address.Capabilities = [.. address.GetCapabilities(), OmnichannelConstants.Channels.Phone];
                    await _addressManager.UpdateAsync(address);
                }

                if (Assign(entryPoint, address, entryPoints))
                {
                    moved++;
                }
                else
                {
                    skipped++;
                }
            }

            entryPoint.DialedNumbers = [];
            changedEntryPoints.Add(entryPoint.ItemId);
        }

        // 3. The number a queue named as its own.
        foreach (var queueManager in _queueManagers.Take(1))
        {
            foreach (var queue in (await queueManager.GetAllAsync()).Where(queue => !string.IsNullOrEmpty(queue.InboundChannelEndpointId)))
            {
                var address = addresses.FirstOrDefault(candidate => candidate.IsKnownAs(queue.InboundChannelEndpointId));

                if (address is not null && !entryPoints.Any(entryPoint => IsCallEntryPoint(entryPoint) && entryPoint.Enabled && entryPoint.AddressIds.Any(address.IsKnownAs)))
                {
                    var entryPoint = await _entryPointManager.NewAsync();
                    entryPoint.Name = await UniqueNameAsync(queue.Name ?? address.DisplayText ?? address.Value, entryPoints);
                    entryPoint.Description = $"Created from the queue's inbound number when numbers moved to entry points.";
                    entryPoint.Channel = OmnichannelConstants.Channels.Phone;
                    entryPoint.AddressIds = [address.ItemId];
                    entryPoint.TargetType = Models.EntryPointTargetType.Queue;
                    entryPoint.TargetQueueId = queue.ItemId;
                    entryPoint.Enabled = queue.Enabled;
                    entryPoint.CreatedUtc = DateTime.UtcNow;

                    await _entryPointManager.CreateAsync(entryPoint);
                    entryPoints.Add(entryPoint);
                    moved++;
                }

                queue.InboundChannelEndpointId = null;
                await queueManager.UpdateAsync(queue);
            }
        }

        foreach (var entryPoint in entryPoints.Where(entryPoint => changedEntryPoints.Contains(entryPoint.ItemId)))
        {
            await _entryPointManager.UpdateAsync(entryPoint);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Moved {Moved} number(s) onto inbound entry points, created {Created} address(es) for typed numbers, and left {Skipped} typed number(s) that another entry point already answers.",
                moved,
                created,
                skipped);
        }

        return 1;
    }

    // Gives a number to an entry point unless another enabled call entry point already answers it.
    private bool Assign(ContactCenterEntryPoint entryPoint, OmnichannelChannelEndpoint address, IEnumerable<ContactCenterEntryPoint> entryPoints)
    {
        if (entryPoint.AddressIds.Any(address.IsKnownAs))
        {
            return true;
        }

        var holder = entryPoints.FirstOrDefault(other => other != entryPoint && other.Enabled && entryPoint.Enabled &&
            IsCallEntryPoint(other) && other.AddressIds.Any(address.IsKnownAs));

        if (holder is not null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Entry point '{EntryPoint}' listed {Number}, which entry point '{Holder}' already answers; it was not moved to '{EntryPoint}'.",
                    entryPoint.Name.SanitizeLogValue(),
                    address.Value.SanitizeLogValue(),
                    holder.Name.SanitizeLogValue(),
                    entryPoint.Name.SanitizeLogValue());
            }

            return false;
        }

        entryPoint.AddressIds.Add(address.ItemId);

        return true;
    }

    private async Task<string> UniqueNameAsync(string name, IEnumerable<ContactCenterEntryPoint> entryPoints)
    {
        var candidate = name;
        var suffix = 2;

        while (entryPoints.Any(entryPoint => string.Equals(entryPoint.Name, candidate, StringComparison.OrdinalIgnoreCase)) ||
            await _entryPointManager.FindByNameAsync(candidate) is not null)
        {
            candidate = $"{name} ({suffix++})";
        }

        return candidate;
    }

    private static bool IsCallEntryPoint(ContactCenterEntryPoint entryPoint)
        => string.Equals(entryPoint.GetChannel(), OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase);

    private string Canonicalize(string number)
        => _phoneNumberService.TryParse(number.Trim(), null, out var canonical) ? canonical.Value : number.Trim();

    private static string ReadEntryPointId(object settings)
    {
        try
        {
            var json = settings is JsonElement element ? element : JsonSerializer.SerializeToElement(settings);

            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("EntryPointId", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
