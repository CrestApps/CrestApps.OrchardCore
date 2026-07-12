using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelActivityBatchAIProfileViewModel
{
    /// <summary>
    /// Gets or sets the activity source.
    /// </summary>
    [BindNever]
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the AI profile identifier used by automated activities.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the available AI profiles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AIProfiles { get; set; }
}
