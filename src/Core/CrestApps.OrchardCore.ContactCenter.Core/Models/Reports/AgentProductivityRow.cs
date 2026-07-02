namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the productivity of a single agent over a reporting period.
/// </summary>
public sealed class AgentProductivityRow
{
    /// <summary>
    /// Gets or sets the agent profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the resolved display name of the agent.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the number of interactions the agent handled (answered).
    /// </summary>
    public long InteractionsHandled { get; set; }

    /// <summary>
    /// Gets or sets the number of inbound interactions the agent handled.
    /// </summary>
    public long InboundHandled { get; set; }

    /// <summary>
    /// Gets or sets the number of outbound interactions the agent handled.
    /// </summary>
    public long OutboundHandled { get; set; }

    /// <summary>
    /// Gets or sets the total talk time, in seconds, across the agent's handled interactions.
    /// </summary>
    public double TotalTalkTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the average handle time, in seconds, across the agent's handled interactions.
    /// </summary>
    public double AverageHandleTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of CRM activities the agent completed.
    /// </summary>
    public long ActivitiesCompleted { get; set; }
}
