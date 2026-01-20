namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class ListOmnichannelActivityFilter
{
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    public string SubjectContentType { get; set; }

    public string Channel { get; set; }

    public int? AttemptFrom { get; set; }

    public int? AttemptTo { get; set; }
}
