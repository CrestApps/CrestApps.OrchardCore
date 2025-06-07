using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class AIChatWidgetViewModel
{
    public string ProfileId { get; set; }

    public int? TotalHistory { get; set; }

    [BindNever]
    public int MaxHistoryAllowed { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }
}
