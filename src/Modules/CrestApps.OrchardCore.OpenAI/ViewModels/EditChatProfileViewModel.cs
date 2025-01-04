using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class EditChatProfileViewModel
{
    public string Name { get; set; }

    public string SystemMessage { get; set; }

    public string WelcomeMessage { get; set; }

    public string PromptTemplate { get; set; }

    public string PromptSubject { get; set; }

    public string DeploymentId { get; set; }

    public OpenAIChatProfileType ProfileType { get; set; }

    public OpenAISessionTitleType? TitleType { get; set; }

    public FunctionEntry[] Functions { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public IList<SelectListItem> Deployments { get; set; }

    [BindNever]
    public IList<SelectListItem> TitleTypes { get; set; }

    [BindNever]
    public IList<SelectListItem> ProfileTypes { get; set; }
}

public class FunctionEntry
{
    public string Name { get; set; }

    public string Description { get; set; }

    public bool IsSelected { get; set; }
}
