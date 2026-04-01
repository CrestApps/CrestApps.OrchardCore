using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditResponseHandlerProfileSettingsViewModel
{
    public string InitialResponseHandlerName { get; set; }

    [BindNever]
    public IList<SelectListItem> ResponseHandlers { get; set; }
}
