using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditChatProfileViewModel
{
    public string Name { get; set; }

    public string WelcomeMessage { get; set; }

    public string PromptTemplate { get; set; }

    public string PromptSubject { get; set; }

    public string ConnectionName { get; set; }

    public string DeploymentId { get; set; }

    public bool IsOnAdminMenu { get; set; }

    public AIChatProfileType ProfileType { get; set; }

    public AISessionTitleType? TitleType { get; set; }

    public FunctionEntry[] Functions { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public IList<SelectListItem> ConnectionNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; }

    [BindNever]
    public IList<SelectListItem> TitleTypes { get; set; }

    [BindNever]
    public IList<SelectListItem> ProfileTypes { get; set; }

    [BindNever]
    public string Source { get; set; }
}

public class FunctionEntry
{
    public string Name { get; set; }

    public string Description { get; set; }

    public bool IsSelected { get; set; }
}
