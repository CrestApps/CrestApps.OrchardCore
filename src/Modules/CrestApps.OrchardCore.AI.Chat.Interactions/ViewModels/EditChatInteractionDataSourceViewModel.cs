using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class EditChatInteractionDataSourceViewModel
{
    public string DataSourceId { get; set; }

    public IEnumerable<SelectListItem> DataSources { get; set; } = [];
}
