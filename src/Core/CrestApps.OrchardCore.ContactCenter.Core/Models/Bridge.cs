using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the media topology that joins a call session's legs, together with the full membership
/// history of that topology.
/// </summary>
public sealed class Bridge
{
    /// <summary>
    /// Gets or sets the provider identifier of the topology. Providers name this differently, so the value
    /// is opaque here and is only ever compared for equality or presence.
    /// </summary>
    public string ProviderBridgeId { get; set; }

    /// <summary>
    /// Gets or sets the shape of the topology.
    /// </summary>
    public BridgeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the topology was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the topology was destroyed.
    /// </summary>
    public DateTime? DestroyedUtc { get; set; }

    /// <summary>
    /// Gets or sets the append-only membership history of the topology.
    /// </summary>
    public IList<BridgeParticipant> Participants { get; set; } = [];

    /// <summary>
    /// Gets or sets the live participant count the provider reports, when it reports one. It is kept apart
    /// from <see cref="Participants"/> because a provider that publishes only a number cannot say who those
    /// parties are, and inventing members to match a count would make the membership history a fiction.
    /// </summary>
    public int? ReportedParticipantCount { get; set; }

    /// <summary>
    /// Gets the participants that are currently present on the topology.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<BridgeParticipant> ActiveParticipants
        => Participants.Where(participant => participant.LeftUtc is null);

    /// <summary>
    /// Gets the participants that were present on the topology at the given instant.
    /// </summary>
    /// <param name="instant">The UTC instant to reconstruct membership for.</param>
    /// <returns>The participants whose membership window contains <paramref name="instant"/>.</returns>
    public IEnumerable<BridgeParticipant> ParticipantsAt(DateTime instant)
    {
        return Participants.Where(participant =>
            participant.JoinedUtc <= instant &&
            (participant.LeftUtc is null || participant.LeftUtc > instant));
    }
}
