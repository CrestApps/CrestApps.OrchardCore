using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;

/// <summary>
/// Moves where each messaging number's messages go off the number and onto an inbound entry point for its channel, the
/// one place a number's inbound traffic is routed from.
/// </summary>
/// <remarks>
/// The routing a number carried becomes an enabled entry point named after the number, answering it on each messaging
/// channel it is used for, with the same target, distribution and auto-reply. A number with no target routed nowhere,
/// so it gets no entry point; a number another entry point already answers on the channel is left to it. Either is
/// logged. The settings are removed from the number in every case, so the upgrade runs once.
/// </remarks>
internal sealed class MessagingEntryPointMigrations : DataMigration
{
    // The name the SMS portal stored a number's routing under, before the messaging workspace replaced it.
    private const string LegacyRoutingSettingsKey = "SmsEndpointRoutingSettings";

    // The portal wrote its enums by name.
    private static readonly System.Text.Json.JsonSerializerOptions _legacyOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly IContactCenterEntryPointManager _entryPointManager;
    private readonly IOmnichannelChannelEndpointManager _addressManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingEntryPointMigrations"/> class.
    /// </summary>
    /// <param name="entryPointManager">The entry points.</param>
    /// <param name="addressManager">The address list.</param>
    /// <param name="logger">The logger.</param>
    public MessagingEntryPointMigrations(
        IContactCenterEntryPointManager entryPointManager,
        IOmnichannelChannelEndpointManager addressManager,
        ILogger<MessagingEntryPointMigrations> logger)
    {
        _entryPointManager = entryPointManager;
        _addressManager = addressManager;
        _logger = logger;
    }

    /// <summary>
    /// Moves the numbers' routing onto entry points.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await MoveRoutingToEntryPointsAsync(_entryPointManager, _addressManager, _logger);

        return 2;
    }

    /// <summary>
    /// Moves the routing the first pass left behind: a number whose routing was still stored under the SMS portal's
    /// name, because the portal import never ran on its tenant.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await MoveRoutingToEntryPointsAsync(_entryPointManager, _addressManager, _logger);

        return 2;
    }

    /// <summary>
    /// Moves the routing the messaging numbers carry onto entry points. The SMS portal import calls it too, because it
    /// copies the portal's routing onto the numbers after this migration has run.
    /// </summary>
    /// <param name="serviceProvider">The tenant's services.</param>
    /// <returns>A task that completes when the routing has moved.</returns>
    internal static Task MoveRoutingToEntryPointsAsync(IServiceProvider serviceProvider)
        => MoveRoutingToEntryPointsAsync(
            serviceProvider.GetRequiredService<IContactCenterEntryPointManager>(),
            serviceProvider.GetRequiredService<IOmnichannelChannelEndpointManager>(),
            serviceProvider.GetRequiredService<ILogger<MessagingEntryPointMigrations>>());

    internal static async Task MoveRoutingToEntryPointsAsync(
        IContactCenterEntryPointManager entryPointManager,
        IOmnichannelChannelEndpointManager addressManager,
        ILogger logger)
    {
        var key = nameof(MessagingEndpointRoutingSettings);
        var addresses = (await addressManager.GetAllAsync())
            .Where(address => address.Properties?.ContainsKey(key) == true || address.Properties?.ContainsKey(LegacyRoutingSettingsKey) == true)
            .ToList();

        if (addresses.Count == 0)
        {
            return;
        }

        var entryPoints = (await entryPointManager.GetAllAsync()).ToList();
        var created = 0;

        foreach (var address in addresses)
        {
            // The SMS portal stored the same settings under its own name; a tenant whose portal import never ran still has
            // them there, and the workspace's name wins when both are present.
            address.TryGet<MessagingEndpointRoutingSettings>(out var routing);

            if (routing is null && address.Properties.TryGetValue(LegacyRoutingSettingsKey, out var legacy))
            {
                routing = ReadLegacy(legacy);
            }

            if (routing is null || string.IsNullOrWhiteSpace(routing.TargetId))
            {
                if (!string.IsNullOrWhiteSpace(routing?.AutoReplyMessage) && logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(
                        "{Number} had an auto-reply but routed its messages nowhere, so no entry point was created for it. Add an entry point for it to keep the auto-reply.",
                        address.Value.SanitizeLogValue());
                }
            }
            else
            {
                foreach (var channel in MessagingChannelsOf(address))
                {
                    var holder = entryPoints.FirstOrDefault(entryPoint => entryPoint.Enabled &&
                        string.Equals(entryPoint.GetChannel(), channel, StringComparison.OrdinalIgnoreCase) &&
                        (entryPoint.AddressIds ?? []).Any(address.IsKnownAs));

                    if (holder is not null)
                    {
                        if (logger.IsEnabled(LogLevel.Warning))
                        {
                            logger.LogWarning(
                                "{Number} is already answered by the entry point '{EntryPoint}' for {Channel}; its own routing was not moved.",
                                address.Value.SanitizeLogValue(),
                                holder.Name.SanitizeLogValue(),
                                channel);
                        }

                        continue;
                    }

                    var entryPoint = await entryPointManager.NewAsync();
                    var isAgent = routing.TargetType == ConversationRouteTargetType.Agent;

                    entryPoint.Name = await UniqueNameAsync(entryPointManager, address, channel, entryPoints);
                    entryPoint.Description = "Created from the number's own routing when messaging numbers moved to entry points.";
                    entryPoint.Channel = channel;
                    entryPoint.AddressIds = [address.ItemId];
                    entryPoint.TargetType = isAgent ? EntryPointTargetType.Agent : EntryPointTargetType.Queue;
                    entryPoint.TargetAgentId = isAgent ? routing.TargetId : null;
                    entryPoint.TargetQueueId = isAgent ? null : routing.TargetId;
                    entryPoint.Enabled = true;
                    entryPoint.CreatedUtc = DateTime.UtcNow;
                    entryPoint.Put(new MessagingEntryPointSettings
                    {
                        DistributionMode = routing.DistributionMode,
                        AutoReplyMessage = string.IsNullOrWhiteSpace(routing.AutoReplyMessage) ? null : routing.AutoReplyMessage.Trim(),
                    });

                    await entryPointManager.CreateAsync(entryPoint);
                    entryPoints.Add(entryPoint);
                    created++;
                }
            }

            address.Properties.Remove(key);
            address.Properties.Remove(LegacyRoutingSettingsKey);
            await addressManager.UpdateAsync(address);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Moved the routing of {Count} messaging number(s) onto {Created} new inbound entry point(s).",
                addresses.Count,
                created);
        }
    }

    private static MessagingEndpointRoutingSettings ReadLegacy(object value)
    {
        try
        {
            var json = value is System.Text.Json.JsonElement element ? element.GetRawText() : System.Text.Json.JsonSerializer.Serialize(value);

            return System.Text.Json.JsonSerializer.Deserialize<MessagingEndpointRoutingSettings>(json, _legacyOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    // The messaging channels the number is used for. A number whose routing was saved before addresses had capabilities
    // was a text number.
    private static List<string> MessagingChannelsOf(OmnichannelChannelEndpoint address)
    {
        var channels = address.GetCapabilities()
            .Where(capability => !string.Equals(capability, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return channels.Count > 0 ? channels : [OmnichannelConstants.Channels.Sms];
    }

    private static async Task<string> UniqueNameAsync(
        IContactCenterEntryPointManager entryPointManager,
        OmnichannelChannelEndpoint address,
        string channel,
        IEnumerable<ContactCenterEntryPoint> entryPoints)
    {
        var name = string.IsNullOrWhiteSpace(address.DisplayText) ? address.Value : address.DisplayText.Trim();

        async Task<bool> TakenAsync(string candidate)
            => entryPoints.Any(entryPoint => string.Equals(entryPoint.Name, candidate, StringComparison.OrdinalIgnoreCase)) ||
                await entryPointManager.FindByNameAsync(candidate) is not null;

        if (!await TakenAsync(name))
        {
            return name;
        }

        // The number's call entry point usually carries its name already.
        var withChannel = $"{name} ({channel})";
        var candidate = withChannel;
        var suffix = 2;

        while (await TakenAsync(candidate))
        {
            candidate = $"{withChannel} {suffix++}";
        }

        return candidate;
    }
}
