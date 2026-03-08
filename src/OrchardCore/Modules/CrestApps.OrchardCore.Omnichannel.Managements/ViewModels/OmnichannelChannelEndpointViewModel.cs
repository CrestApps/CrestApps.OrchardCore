using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelChannelEndpointViewModel
{
    public string DisplayText { get; set; }

    public string Description { get; set; }

    public string Channel { get; set; }

    public string Value { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; }
}
