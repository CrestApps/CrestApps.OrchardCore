using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class StdioConnectionFieldsViewModel
{
    public string Command { get; set; }

    public string Arguments { get; set; }

    [BindNever]
    public string Schema { get; set; }
}
