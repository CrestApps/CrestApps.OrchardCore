using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class ChatAnalyticsProfileFilterViewModel
{
    public string ProfileId { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }
}
