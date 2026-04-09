using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Memory.ViewModels;

public class AIMemorySettingsViewModel
{
    public string IndexProfileName { get; set; }

    public int TopN { get; set; } = 5;

    [BindNever]
    public IEnumerable<SelectListItem> IndexProfiles { get; set; } = [];
}
