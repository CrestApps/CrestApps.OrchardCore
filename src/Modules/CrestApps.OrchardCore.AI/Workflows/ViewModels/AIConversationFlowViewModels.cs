using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// The editor of a profile's conversation flow.
/// </summary>
public class AIProfileFlowViewModel
{
    /// <summary>
    /// Gets or sets the flow's workflow type; empty for none.
    /// </summary>
    public string WorkflowTypeId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every stage's instructions are given at once.
    /// </summary>
    public bool Flexible { get; set; }

    /// <summary>
    /// Gets or sets the flows to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Flows { get; set; } = [];
}

/// <summary>
/// The editor of the AI Conversation Stage task.
/// </summary>
public class AIConversationStageTaskViewModel
{
    /// <summary>
    /// Gets or sets the stage's name.
    /// </summary>
    public string StageName { get; set; }

    /// <summary>
    /// Gets or sets the Liquid template of the instructions.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the ways out, one per line.
    /// </summary>
    public string Outcomes { get; set; }
}
