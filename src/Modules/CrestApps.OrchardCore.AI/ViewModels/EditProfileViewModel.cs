using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditProfileViewModel
{
    public string WelcomeMessage { get; set; }

    public string PromptTemplate { get; set; }

    public string PromptSubject { get; set; }

    public bool IsOnAdminMenu { get; set; }

    public AIProfileType ProfileType { get; set; }

    public AISessionTitleType? TitleType { get; set; }

    public FunctionEntry[] Functions { get; set; }

    [BindNever]
    public IList<SelectListItem> TitleTypes { get; set; }

    [BindNever]
    public IList<SelectListItem> ProfileTypes { get; set; }
}
