using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel activity batch.
/// </summary>
public sealed class OmnichannelActivityBatch : CatalogItem, IDisplayTextAwareModel, ICloneable<OmnichannelActivityBatch>
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
    /// Gets or sets the subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the contact content type.
    /// </summary>
    public string ContactContentType { get; set; }

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
    /// Creates a copy of the current activity batch.
    /// </summary>
    public OmnichannelActivityBatch Clone()
    {
        return new OmnichannelActivityBatch
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            CampaignId = CampaignId,
            SubjectContentType = SubjectContentType,
            ContactContentType = ContactContentType,
            UserIds = UserIds?.ToArray(),
            IncludeDoNoCalls = IncludeDoNoCalls,
            IncludeDoNoSms = IncludeDoNoSms,
            IncludeDoNoEmail = IncludeDoNoEmail,
            CreatedUtc = CreatedUtc,
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
        };
    }
}
