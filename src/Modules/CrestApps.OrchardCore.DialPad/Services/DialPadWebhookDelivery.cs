using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.DialPad.Services;

internal static class DialPadWebhookDelivery
{
    public static string GetDeliveryId(DialPadCallEvent callEvent)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        var value = string.Join(
            '|',
            callEvent.CallId,
            callEvent.State,
            callEvent.EventTimestamp,
            callEvent.IsMuted,
            callEvent.RecordingState,
            callEvent.RecordingId,
            callEvent.IsConference,
            callEvent.ParticipantCount);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return $"{DialPadConstants.ProviderTechnicalName}:{Convert.ToHexString(hash)}";
    }
}
