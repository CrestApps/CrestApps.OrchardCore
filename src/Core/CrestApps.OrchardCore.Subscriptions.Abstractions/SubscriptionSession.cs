using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions;

public sealed class SubscriptionSession : Entity
{
    public string SessionId { get; set; }

    public string ContentItemId { get; set; }

    public string ContentItemVersionId { get; set; }

    public SubscriptionSessionStatus Status { get; set; }

    public bool InitialPaymentAmount { get; set; }

    public bool BillingAmount { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string OwnerId { get; set; }

    public Dictionary<string, object> SavedSteps { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public IList<SubscriptionFlowStep> Steps { get; init; } = [];

    public string CurrentStep { get; set; }

    public string IPAddress { get; set; }

    public string AgentInfo { get; set; }
}

public enum SubscriptionSessionStatus
{
    Pending,
    Canceled,
    Suspended,
    Completed,
}
