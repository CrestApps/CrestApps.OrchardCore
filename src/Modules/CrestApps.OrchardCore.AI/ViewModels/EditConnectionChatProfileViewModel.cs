using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditConnectionChatProfileViewModel
{
    public string ConnectionName { get; set; }

    [BindNever]
    public IList<SelectListItem> ConnectionNames { get; set; }
}
