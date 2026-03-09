using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIProfileTemplateProfileFieldsViewModel
{
    public string SystemMessage { get; set; }

    public string WelcomeMessage { get; set; }

    public string PromptTemplate { get; set; }

    public string PromptSubject { get; set; }

    public AIProfileType? ProfileType { get; set; }

    public AISessionTitleType? TitleType { get; set; }

    public string ConnectionName { get; set; }

    public string OrchestratorName { get; set; }
}
