using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class OpenAIChatWidgetViewModel
{
    public string ProfileId { get; set; }

    public int? TotalHistory { get; set; }

    [BindNever]
    public int MaxHistoryAllowed { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }
}
