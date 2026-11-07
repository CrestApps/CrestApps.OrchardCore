using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Sources;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Messaging broadcasts travel between sites as drafts: the export leaves out the delivery, the import never queues a
/// message, a second run adds nothing, and a broadcast that has been sent is left as it is.
/// </summary>
public sealed class MessagingBroadcastsRecipeTests
{
    private const string BroadcastId = "4bc00000000000000000000001";

    [Fact]
    public async Task Broadcasts_RoundTripIntoAnEmptySite_AsDrafts()
    {
        // Arrange
        var source = new InMemoryBroadcastManager();

        source.Entries.Add(new MessagingBroadcast
        {
            ItemId = BroadcastId,
            Name = "Holiday hours",
            Channel = "sms",
            ServiceAddress = "+12025550100",
            Body = "We close at noon on Friday.",
            Recipients = ["+12025550101", "+12025550102"],
            ProcessedRecipients = ["+12025550101"],
            Status = MessagingBroadcastStatus.Queued,
            SentCount = 1,
            SkippedCount = 1,
            OutreachRunId = "4bc00000000000000000000run",
        });

        var target = new InMemoryBroadcastManager();
        var step = new MessagingBroadcastsStep(target, RecipeRoundTrip.Localizer<MessagingBroadcastsStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new MessagingBroadcastsDeploymentSource(source), new MessagingBroadcastsDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal("MessagingBroadcasts", exported["name"].GetValue<string>());
        Assert.False(exported["Broadcasts"][0].AsObject().ContainsKey("ProcessedRecipients"));
        Assert.False(exported["Broadcasts"][0].AsObject().ContainsKey("SkippedCount"));
        Assert.False(exported["Broadcasts"][0].AsObject().ContainsKey("OutreachRunId"));

        var imported = Assert.Single(target.Entries);

        Assert.Equal(BroadcastId, imported.ItemId);
        Assert.Equal("We close at noon on Friday.", imported.Body);
        Assert.Equal(["+12025550101", "+12025550102"], imported.Recipients);
        Assert.Equal(MessagingBroadcastStatus.Draft, imported.Status);
        Assert.Empty(imported.ProcessedRecipients);
        Assert.Equal(0, imported.SentCount);
        Assert.Equal(0, imported.SkippedCount);
        Assert.Null(imported.OutreachRunId);
    }

    [Fact]
    public async Task ASentBroadcast_IsLeftAsItIs()
    {
        // Arrange
        var target = new InMemoryBroadcastManager();

        target.Entries.Add(new MessagingBroadcast { ItemId = BroadcastId, Name = "Sale", Channel = "sms", ServiceAddress = "+12025550100", Body = "Sale today", Recipients = ["+12025550101"], Status = MessagingBroadcastStatus.Queued });

        var step = new MessagingBroadcastsStep(target, RecipeRoundTrip.Localizer<MessagingBroadcastsStep>());
        var recipe = JsonNode.Parse($$"""
            { "name": "MessagingBroadcasts", "Broadcasts": [ { "ItemId": "{{BroadcastId}}", "Name": "Sale", "Channel": "sms", "ServiceAddress": "+12025550100", "Body": "Changed", "Recipients": [ "+12025550101" ] } ] }
            """).AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);
        Assert.Equal("Sale today", Assert.Single(target.Entries).Body);
    }

    private sealed class InMemoryBroadcastManager : IMessagingBroadcastManager
    {
        public List<MessagingBroadcast> Entries { get; } = [];

        public ValueTask<MessagingBroadcast> FindByIdAsync(string id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Entries.FirstOrDefault(entry => entry.ItemId == id));

        public ValueTask<IEnumerable<MessagingBroadcast>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IEnumerable<MessagingBroadcast>>(Entries.ToList());

        public ValueTask<PageResult<MessagingBroadcast>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
            where TQuery : QueryContext
            => throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(MessagingBroadcast model, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Entries.Remove(model));

        public ValueTask<MessagingBroadcast> NewAsync(JsonNode data = null, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new MessagingBroadcast { ItemId = UniqueId.GenerateId() });

        public ValueTask CreateAsync(MessagingBroadcast model, CancellationToken cancellationToken = default)
        {
            Entries.Add(model);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(MessagingBroadcast model, JsonNode data = null, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<ValidationResultDetails> ValidateAsync(MessagingBroadcast model, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new ValidationResultDetails());
    }
}
