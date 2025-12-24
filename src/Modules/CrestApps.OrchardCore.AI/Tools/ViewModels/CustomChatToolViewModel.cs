using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Tools.ViewModels;

public sealed class CustomChatToolViewModel
{
    public string CustomChatInstanceId { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Instances { get; set; }
}
