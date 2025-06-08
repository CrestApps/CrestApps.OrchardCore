using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class SseConnectionFieldsViewModel
{
    public string Endpoint { get; set; }

    public string AdditionalHeaders { get; set; }

    [BindNever]
    public string Schema { get; set; }
}
