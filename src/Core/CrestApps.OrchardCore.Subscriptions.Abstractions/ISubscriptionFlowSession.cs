using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions;

public interface ISubscriptionFlowSession
{
    string SessionId { get; set; }

    string CurrentStep { get; set; }

    IList<SubscriptionFlowStep> Steps { get; }

    JsonObject SavedSteps { get; }
}
