using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskRealtimeVoiceEvent
{
    public string ProviderName { get; set; }

    public string CallId { get; set; }

    public string EventType { get; set; }

    public string FromAddress { get; set; }

    public string ToAddress { get; set; }

    public CallState State { get; set; }

    public bool? IsMuted { get; set; }

    public bool IsOnHold { get; set; }

    public DateTime? OccurredUtc { get; set; }

    public string IdempotencyKey { get; set; }

    public bool? IsConference { get; set; }

    public int? ParticipantCount { get; set; }

    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
