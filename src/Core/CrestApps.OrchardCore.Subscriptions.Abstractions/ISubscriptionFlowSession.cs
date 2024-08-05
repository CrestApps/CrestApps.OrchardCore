using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions;

public interface ISubscriptionFlowSession : IEntity
{
    string SessionId { get; set; }

    string CurrentStep { get; set; }

    SubscriptionSessionStatus Status { get; set; }

    IList<SubscriptionFlowStep> Steps { get; }

    JsonObject SavedSteps { get; }
}
