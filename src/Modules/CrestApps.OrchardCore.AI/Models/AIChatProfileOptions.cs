using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Models;

public class AIChatProfileOptions
{
    public string Search { get; set; }

    public AIChatProfileAction BulkAction { get; set; }

    [BindNever]
    public List<SelectListItem> BulkActions { get; set; }
}
