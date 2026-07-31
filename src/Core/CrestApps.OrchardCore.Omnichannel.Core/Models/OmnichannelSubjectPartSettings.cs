namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the type-level flow settings for <see cref="OmnichannelSubjectPart"/>. These settings are
/// stored on the content-type part definition and describe the stable configuration of a subject.
/// Volatile, per-run values (such as the outbound channel, channel endpoint, and campaign) are chosen when
/// an activity batch is loaded rather than being fixed on the subject type.
/// </summary>
public sealed class OmnichannelSubjectPartSettings
{
    /// <summary>
    /// Gets or sets the primary communication direction for the subject. New subjects default to
    /// <see cref="SubjectDirection.Outbound"/>.
    /// </summary>
    public SubjectDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the interaction type used for inbound activities of this subject. This value is only
    /// meaningful when <see cref="Direction"/> is <see cref="SubjectDirection.Inbound"/>; for outbound work
    /// the interaction type is derived when the activity batch is loaded.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the communication channel used for inbound activities of this subject. This value is
    /// only meaningful when <see cref="Direction"/> is <see cref="SubjectDirection.Inbound"/>; for outbound
    /// work the channel is chosen when the activity batch is loaded.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint used for inbound activities of this subject. This value is only
    /// meaningful when <see cref="Direction"/> is <see cref="SubjectDirection.Inbound"/>; for outbound work
    /// the endpoint is chosen when the activity batch is loaded.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the optional default campaign for the subject. It is used as the pre-selected campaign
    /// when an activity batch is loaded and may be overridden per batch. Campaigns are used for grouping and
    /// reporting only.
    /// </summary>
    public string DefaultCampaignId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a disposition must be selected before an activity using this
    /// subject can be completed. This is the single decision-control policy that applies to both inbound and
    /// outbound activities and is enforced by the activity disposition service.
    /// </summary>
    public bool RequireDisposition { get; set; }
}
