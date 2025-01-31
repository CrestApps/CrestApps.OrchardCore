using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIChatListOptions
{
    [FromQuery(Name = "q")]
    public string SearchText { get; set; }

    [BindNever]
    public RouteValueDictionary RouteValues { get; set; } = [];
}
