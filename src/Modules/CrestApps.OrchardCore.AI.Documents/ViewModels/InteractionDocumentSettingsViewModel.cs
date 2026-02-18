using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class InteractionDocumentSettingsViewModel
{
    public string IndexProfileName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> IndexProfiles { get; set; }
}
