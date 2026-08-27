using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Builds the <see cref="TelephonyCall"/> that represents a ringing inbound Contact Center voice
/// interaction. It is the single source of truth for that mapping so the live soft-phone ring (pushed
/// from the reservation event) and the current-offer recovery poll surface byte-for-byte the same call,
/// which keeps the incoming-call modal idempotent when both paths reach the same client.
/// </summary>
internal static class ContactCenterIncomingCallFactory
{
    private const string ServiceAddressMetadataKey = "serviceAddress";

    /// <summary>
    /// Builds the ringing inbound call for the specified interaction.
    /// </summary>
    /// <param name="interaction">The inbound voice interaction that is currently ringing an agent.</param>
    /// <param name="nowUtc">The current UTC instant, used when the interaction has no creation stamp.</param>
    /// <returns>The <see cref="TelephonyCall"/> describing the ringing inbound call.</returns>
    public static TelephonyCall BuildRingingInboundCall(Interaction interaction, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return new TelephonyCall
        {
            CallId = interaction.ProviderInteractionId,
            From = interaction.CustomerAddress,
            To = ResolveServiceAddress(interaction),
            State = CallState.Ringing,
            Direction = CallDirection.Inbound,
            ProviderName = interaction.ProviderName,
            StartedUtc = interaction.CreatedUtc == default
                ? nowUtc
                : new DateTimeOffset(DateTime.SpecifyKind(interaction.CreatedUtc, DateTimeKind.Utc)),
            Metadata = BuildCallMetadata(interaction),
        };
    }

    private static string ResolveServiceAddress(Interaction interaction)
    {
        return interaction.TechnicalMetadata.TryGetValue(ServiceAddressMetadataKey, out var value)
            ? value?.ToString()
            : null;
    }

    private static Dictionary<string, object> BuildCallMetadata(Interaction interaction)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(interaction.CustomerAddress))
        {
            metadata["callerAddress"] = interaction.CustomerAddress;
        }

        var serviceAddress = ResolveServiceAddress(interaction);

        if (!string.IsNullOrWhiteSpace(serviceAddress))
        {
            metadata["calledAddress"] = serviceAddress;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ProviderName))
        {
            metadata["providerName"] = interaction.ProviderName;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ItemId))
        {
            metadata["interactionId"] = interaction.ItemId;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ActivityItemId))
        {
            metadata["activityItemId"] = interaction.ActivityItemId;
        }

        if (!string.IsNullOrWhiteSpace(interaction.QueueId))
        {
            metadata["queueId"] = interaction.QueueId;
        }

        return metadata;
    }
}
