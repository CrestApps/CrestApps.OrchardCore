using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Sources;

/// <summary>
/// Writes every messaging broadcast into a <c>MessagingBroadcasts</c> recipe step, without its delivery: the status, the
/// recipients already sent to and the counts belong to the site that sent it.
/// </summary>
internal sealed class MessagingBroadcastsDeploymentSource : CatalogDeploymentSource<MessagingBroadcastsDeploymentStep, MessagingBroadcast>
{
    private static readonly string[] _deliveryMembers =
    [
        nameof(MessagingBroadcast.Status),
        nameof(MessagingBroadcast.ProcessedRecipients),
        nameof(MessagingBroadcast.SentCount),
        nameof(MessagingBroadcast.FailedCount),
        nameof(MessagingBroadcast.CompletedUtc),
    ];

    public MessagingBroadcastsDeploymentSource(IMessagingBroadcastManager manager)
        : base(MessagingBroadcastsStep.EntriesName, manager)
    {
    }

    protected override IEnumerable<MessagingBroadcast> Order(IEnumerable<MessagingBroadcast> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

    protected override JsonObject Export(MessagingBroadcast entry)
    {
        var node = CatalogDeploymentSerializer.Export(entry);

        foreach (var member in _deliveryMembers)
        {
            node.Remove(member);
        }

        return node;
    }
}
