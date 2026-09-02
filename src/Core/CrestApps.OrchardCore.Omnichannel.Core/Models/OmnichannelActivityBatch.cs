using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel activity batch.
/// </summary>
public sealed class OmnichannelActivityBatch : CatalogItem, IDisplayTextAwareModel, IModifiedUtcAwareModel, ICloneable<OmnichannelActivityBatch>
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the campaign id.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the communication channel used for outbound activities loaded from this batch. When
    /// empty, the channel configured on the subject content-type part settings is used as a fallback.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint used for outbound activities loaded from this batch. When empty,
    /// the endpoint configured on the subject content-type part settings is used as a fallback.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the activity source used when loading activities from this batch.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the contact content type.
    /// </summary>
    public string ContactContentType { get; set; }

    /// <summary>
    /// Gets or sets the AI profile identifier assigned to automated activities loaded from this batch.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the optional speech-to-text deployment name assigned to automated phone activities.
    /// </summary>
    public string SpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech deployment name assigned to automated phone activities.
    /// </summary>
    public string TextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech voice identifier assigned to automated phone activities.
    /// </summary>
    public string TextToSpeechVoiceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI may update the contact during automated conversations
    /// loaded from this batch. Chosen when the automated inventory is loaded and snapshotted onto each activity.
    /// </summary>
    public bool AllowAIToUpdateContact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI may update the subject during automated conversations
    /// loaded from this batch. Chosen when the automated inventory is loaded and snapshotted onto each activity.
    /// </summary>
    public bool AllowAIToUpdateSubject { get; set; } = true;

    /// <summary>
    /// Gets or sets how long automated conversations loaded from this batch wait before sending each AI reply, so
    /// responses do not feel instant. Snapshotted onto each activity when the inventory is loaded.
    /// </summary>
    public OmnichannelResponseDelayMode ResponseDelayMode { get; set; }

    /// <summary>
    /// Gets or sets the reply delay in seconds. For <see cref="OmnichannelResponseDelayMode.Fixed"/> this is the exact
    /// wait; for <see cref="OmnichannelResponseDelayMode.Random"/> this is the base the jitter is applied around.
    /// </summary>
    public int ResponseDelaySeconds { get; set; }

    /// <summary>
    /// Gets or sets the jitter, in seconds, applied around <see cref="ResponseDelaySeconds"/> when
    /// <see cref="ResponseDelayMode"/> is <see cref="OmnichannelResponseDelayMode.Random"/>.
    /// </summary>
    public int ResponseDelayJitterSeconds { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the business-hours calendar that gates background-initiated sends (such as
    /// re-engagement nudges) for conversations loaded from this batch. Evaluated in the contact's local time zone.
    /// When empty, sends are never restricted by hours. Snapshotted onto each activity when the inventory is loaded.
    /// </summary>
    public string BusinessHoursCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the reusable <c>Cadence</c> that defines the re-engagement cadence and
    /// messages for contacts who go quiet. When empty, the automation never nudges. Snapshotted onto each activity when
    /// the inventory is loaded. Every nudge still respects <see cref="BusinessHoursCalendarId"/>.
    /// </summary>
    public string CadenceId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile identifier assigned to dialer activities loaded from this batch.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the user ids.
    /// </summary>
    public string[] UserIds { get; set; }

    /// <summary>
    /// Gets or sets the include do no calls.
    /// </summary>
    public bool IncludeDoNoCalls { get; set; }

    /// <summary>
    /// Gets or sets the include do no sms.
    /// </summary>
    public bool IncludeDoNoSms { get; set; }

    /// <summary>
    /// Gets or sets the include do no email.
    /// </summary>
    public bool IncludeDoNoEmail { get; set; }

    /// <summary>
    /// Gets or sets the created utc.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the modified utc.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner id.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the schedule at.
    /// </summary>
    public DateTime ScheduleAt { get; set; }

    /// <summary>
    /// Gets or sets the instructions.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the total loaded.
    /// </summary>
    public long? TotalLoaded { get; set; }

    /// <summary>
    /// Gets or sets the prevent duplicates.
    /// </summary>
    public bool PreventDuplicates { get; set; }

    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public OmnichannelActivityBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the lead created from.
    /// </summary>
    public DateTime? LeadCreatedFrom { get; set; }

    /// <summary>
    /// Gets or sets the lead created to.
    /// </summary>
    public DateTime? LeadCreatedTo { get; set; }

    /// <summary>
    /// Gets or sets the only published leads.
    /// </summary>
    public bool OnlyPublishedLeads { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of leads to load into the batch.
    /// When <see langword="null"/>, all matching leads are loaded.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the phone number to filter leads by.
    /// A leading plus sign searches E.164 values; otherwise, the national number is searched.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the match type for the phone number filter.
    /// </summary>
    public PhoneNumberMatchType PhoneNumberMatchType { get; set; } = PhoneNumberMatchType.Contains;

    /// <summary>
    /// Gets or sets the time zone identifiers to filter leads by.
    /// </summary>
    public string[] TimeZoneIds { get; set; }

    /// <summary>
    /// Gets or sets the subject content type of the last completed activity to filter leads by.
    /// </summary>
    public string LastActivitySubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the disposition identifier of the last completed activity to filter leads by.
    /// </summary>
    public string LastActivityDispositionId { get; set; }

    /// <summary>
    /// Creates a copy of the current activity batch.
    /// </summary>
    public OmnichannelActivityBatch Clone()
    {
        return new OmnichannelActivityBatch
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            CampaignId = CampaignId,
            Channel = Channel,
            ChannelEndpointId = ChannelEndpointId,
            Source = Source,
            SubjectContentType = SubjectContentType,
            ContactContentType = ContactContentType,
            AIProfileId = AIProfileId,
            SpeechToTextDeploymentName = SpeechToTextDeploymentName,
            TextToSpeechDeploymentName = TextToSpeechDeploymentName,
            TextToSpeechVoiceId = TextToSpeechVoiceId,
            AllowAIToUpdateContact = AllowAIToUpdateContact,
            AllowAIToUpdateSubject = AllowAIToUpdateSubject,
            ResponseDelayMode = ResponseDelayMode,
            ResponseDelaySeconds = ResponseDelaySeconds,
            ResponseDelayJitterSeconds = ResponseDelayJitterSeconds,
            BusinessHoursCalendarId = BusinessHoursCalendarId,
            CadenceId = CadenceId,
            DialerProfileId = DialerProfileId,
            UserIds = UserIds?.ToArray(),
            IncludeDoNoCalls = IncludeDoNoCalls,
            IncludeDoNoSms = IncludeDoNoSms,
            IncludeDoNoEmail = IncludeDoNoEmail,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
            ScheduleAt = ScheduleAt,
            Instructions = Instructions,
            TotalLoaded = TotalLoaded,
            PreventDuplicates = PreventDuplicates,
            UrgencyLevel = UrgencyLevel,
            Status = Status,
            LeadCreatedFrom = LeadCreatedFrom,
            LeadCreatedTo = LeadCreatedTo,
            OnlyPublishedLeads = OnlyPublishedLeads,
            Limit = Limit,
            PhoneNumber = PhoneNumber,
            PhoneNumberMatchType = PhoneNumberMatchType,
            TimeZoneIds = TimeZoneIds?.ToArray(),
            LastActivitySubjectContentType = LastActivitySubjectContentType,
            LastActivityDispositionId = LastActivityDispositionId,
        };
    }
}
