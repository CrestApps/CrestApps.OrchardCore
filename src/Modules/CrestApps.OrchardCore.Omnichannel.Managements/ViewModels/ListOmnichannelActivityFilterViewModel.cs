using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public sealed class ListOmnichannelActivityFilterViewModel
{
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    public string SubjectContentType { get; set; }

    public int? AttemptFrom { get; set; }

    public int? AttemptTo { get; set; }

    public string Channel { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; }
}
