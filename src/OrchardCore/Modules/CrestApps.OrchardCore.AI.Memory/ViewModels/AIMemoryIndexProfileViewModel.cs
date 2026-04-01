using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Memory.ViewModels;

public class AIMemoryIndexProfileViewModel
{
    public string EmbeddingConnection { get; set; }
    [BindNever]
    public IEnumerable<SelectListItem> EmbeddingConnections { get; set; } = [];
}
