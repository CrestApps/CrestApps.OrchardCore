namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// View model for editing the Transfer to Agent workflow activity.
/// </summary>
public sealed class TransferToAgentTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression resolving the CRM activity identifier to transfer.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression resolving the target queue identifier. Optional.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression resolving the escalation reason shown to the agent.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression resolving the conversation summary carried to the agent.
    /// </summary>
    public string Summary { get; set; }
}
