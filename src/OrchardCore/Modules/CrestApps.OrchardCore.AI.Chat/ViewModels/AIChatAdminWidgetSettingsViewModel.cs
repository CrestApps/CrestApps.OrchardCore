using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class AIChatAdminWidgetSettingsViewModel
{
    public string ProfileId { get; set; }

    public int MaxSessions { get; set; }

    public string PrimaryColor { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; } = [];
}
