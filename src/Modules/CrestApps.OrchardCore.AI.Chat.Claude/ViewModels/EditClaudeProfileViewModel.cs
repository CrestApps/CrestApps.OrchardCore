using CrestApps.Core.AI.Claude.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Claude.ViewModels;

public class EditClaudeProfileViewModel
{
    public string ClaudeModel { get; set; }

    public ClaudeEffortLevel ClaudeEffortLevel { get; set; }

    [BindNever]
    public bool IsClaudeConfigured { get; set; }

    [BindNever]
    public IList<SelectListItem> AvailableModels { get; set; }
}
