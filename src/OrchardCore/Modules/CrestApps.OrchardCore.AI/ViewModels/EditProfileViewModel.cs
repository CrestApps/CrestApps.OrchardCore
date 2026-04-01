using CrestApps.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditProfileViewModel
{
    public string WelcomeMessage { get; set; }

    public bool AddInitialPrompt { get; set; }

    public string InitialPrompt { get; set; }

    public string PromptTemplate { get; set; }

    public string PromptSubject { get; set; }

    public string Description { get; set; }

    public AIProfileType ProfileType { get; set; }

    public AgentAvailability AgentAvailability { get; set; }

    public AISessionTitleType? TitleType { get; set; }
    [BindNever]
    public IList<SelectListItem> TitleTypes { get; set; }
    [BindNever]
    public IList<SelectListItem> ProfileTypes { get; set; }
    [BindNever]
    public IList<SelectListItem> AvailabilityTypes { get; set; }
}
