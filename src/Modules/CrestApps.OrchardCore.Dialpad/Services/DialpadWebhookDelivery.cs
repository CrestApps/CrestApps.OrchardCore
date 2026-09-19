using System.Security.Cryptography;
using System.Text;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.OrchardCore.Dialpad.Services;

internal static class DialpadWebhookDelivery
{
    public static string GetDeliveryId(DialpadCallEvent callEvent)
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

        return $"{DialpadConstants.ProviderTechnicalName}:{Convert.ToHexString(hash)}";
    }
}
