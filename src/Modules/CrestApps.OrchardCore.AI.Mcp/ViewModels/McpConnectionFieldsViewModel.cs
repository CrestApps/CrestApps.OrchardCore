using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class McpConnectionFieldsViewModel
{
    public string DisplayText { get; set; }

    public string TransportType { get; set; }

    public string Location { get; set; }

    public string TransportOptions { get; set; }

    [BindNever]
    public string Schema { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> TransportTypes { get; set; }
}
