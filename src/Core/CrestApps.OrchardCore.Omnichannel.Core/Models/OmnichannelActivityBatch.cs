using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelActivityBatch : CatalogEntry, IDisplayTextAwareModel, ICloneable<OmnichannelActivityBatch>
{
    public string DisplayText { get; set; }

    public string CampaignId { get; set; }

    public string Channel { get; set; }

    public string SubjectContentType { get; set; }

    public string ContactContentType { get; set; }

    public string[] UserIds { get; set; }

    public bool IncludeDoNoCalls { get; set; }

    public bool IncludeDoNoSms { get; set; }

    public bool IncludeDoNoEmail { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public DateTime ScheduleAt { get; set; }

    public string Instructions { get; set; }

    public long? TotalLoaded { get; set; }

    public bool PreventDuplicates { get; set; }

    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    public OmnichannelActivityBatchStatus Status { get; set; }

    public OmnichannelActivityBatch Clone()
    {
        return new OmnichannelActivityBatch()
        {
            Id = Id,
            DisplayText = DisplayText,
            CampaignId = CampaignId,
            Channel = Channel,
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
        };
    }
}
