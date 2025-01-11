using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.Workflows.ViewModels;

public class ChatUtilityCompletionTaskViewModel
{
    public string ProfileId { get; set; }

    public string PromptTemplate { get; set; }

    public string ResultPropertyName { get; set; }

    public bool RespondWithHtml { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }
}
