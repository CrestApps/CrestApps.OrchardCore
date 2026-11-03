using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the Simulate AI Conversation task.
/// </summary>
public class SimulateConversationTaskViewModel
{
    /// <summary>
    /// Gets or sets the AI profile under test.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets who the simulated customer is and what they want.
    /// </summary>
    public string Scenario { get; set; }

    /// <summary>
    /// Gets or sets the conversation variables the test starts with.
    /// </summary>
    public string Variables { get; set; }

    /// <summary>
    /// Gets or sets the criteria, one per line.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the most customer turns.
    /// </summary>
    public int MaxTurns { get; set; }

    /// <summary>
    /// Gets or sets the lowest score that passes.
    /// </summary>
    public int PassingScore { get; set; }

    /// <summary>
    /// Gets or sets the label the result is kept under.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the AI profiles to choose from.
    /// </summary>
    /// <summary>
    /// Gets or sets the deployment that plays the customer.
    /// </summary>
    public string CustomerDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the deployment that judges the conversation.
    /// </summary>
    public string JudgeDeploymentName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the deployments that can play the customer or judge.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; } = [];
}
