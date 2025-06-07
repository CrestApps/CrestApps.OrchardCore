using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

public class AICompletionTaskViewModel
{
    public string ProfileId { get; set; }

    public string PromptTemplate { get; set; }

    public string ResultPropertyName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }
}
