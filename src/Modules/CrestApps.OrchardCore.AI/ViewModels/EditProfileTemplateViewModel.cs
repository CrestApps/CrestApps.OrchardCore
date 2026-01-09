using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public sealed class EditProfileTemplateViewModel
{
    public string SelectedTemplate { get; set; }

    public IEnumerable<SelectListItem> AvailableTemplates { get; set; } = [];
}
