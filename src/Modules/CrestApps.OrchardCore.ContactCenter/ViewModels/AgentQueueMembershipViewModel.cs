namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// How an agent serves one of the queues they are entitled to, as edited on the entitlement screen.
/// </summary>
public class AgentQueueMembershipViewModel
{
    public string QueueId { get; set; }

    public string QueueName { get; set; }

    public int Priority { get; set; }

    public int DelaySeconds { get; set; }
}
