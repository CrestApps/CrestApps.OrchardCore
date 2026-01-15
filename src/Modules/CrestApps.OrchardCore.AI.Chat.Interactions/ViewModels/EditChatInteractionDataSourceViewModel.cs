using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class EditChatInteractionDataSourceViewModel
{
    public string DataSourceId { get; set; }

    // Azure RAG parameters
    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public string Filter { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> DataSources { get; set; } = [];
}
