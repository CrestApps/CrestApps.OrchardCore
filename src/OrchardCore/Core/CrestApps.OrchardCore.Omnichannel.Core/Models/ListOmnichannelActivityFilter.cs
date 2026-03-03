using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class ListOmnichannelActivityFilter : Entity
{
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    public string SubjectContentType { get; set; }

    public string Channel { get; set; }

    public string AttemptFilter { get; set; }

    public DateTime? ScheduledFrom { get; set; }

    public DateTime? ScheduledTo { get; set; }

    [BindNever]
    public RouteValueDictionary RouteValues { get; set; } = [];
}
