using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Tools.ViewModels;

public class AIProfileFunctionMetadataViewModel
{
    public string ProfileId { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }
}
