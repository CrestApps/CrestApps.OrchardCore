using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIContactMemoryLeadConversionTests
{
    private static readonly DateTime _now = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LeadConversion_MovesTheLeadsMemoryToTheNewContact()
    {
        var store = new InMemoryMemoryStore();
        store.Items.Add(new AIContactMemory { ItemId = "m1", ContactId = "lead1", Text = "Wants a quote for two cars." });

        await NewRepointer(store).RepointAsync(NewConversion("lead1", "contact1"), TestContext.Current.CancellationToken);

        var memory = Assert.Single(store.Items);
        Assert.Equal("contact1", memory.ContactId);
        Assert.Equal("Wants a quote for two cars.", memory.Text);
    }

    [Fact]
    public async Task LeadConversion_MergesIntoTheMemoryOfAnExistingContact()
    {
        var store = new InMemoryMemoryStore();
        store.Items.Add(new AIContactMemory { ItemId = "m1", ContactId = "lead1", Text = "Wants a quote for two cars." });
        store.Items.Add(new AIContactMemory { ItemId = "m2", ContactId = "contact1", Text = "Prefers texts.", LastSessionId = "s1" });

        await NewRepointer(store).RepointAsync(NewConversion("lead1", "contact1"), TestContext.Current.CancellationToken);

        var memory = Assert.Single(store.Items);
        Assert.Equal("contact1", memory.ContactId);
        Assert.Equal("Prefers texts." + Environment.NewLine + "Wants a quote for two cars.", memory.Text);
        Assert.Equal(_now, memory.UpdatedUtc);
        Assert.Null(memory.LastSessionId);
    }

    [Fact]
    public async Task LeadConversion_LeavesEverythingAloneWithoutALeadMemory()
    {
        var store = new InMemoryMemoryStore();
        store.Items.Add(new AIContactMemory { ItemId = "m2", ContactId = "contact1", Text = "Prefers texts." });

        await NewRepointer(store).RepointAsync(NewConversion("lead1", "contact1"), TestContext.Current.CancellationToken);

        Assert.Equal("Prefers texts.", Assert.Single(store.Items).Text);
    }

    [Theory]
    [InlineData(null, "B", "B")]
    [InlineData("A", null, "A")]
    [InlineData("Knows the dog's name is Rex.", "the dog's name is rex.", "Knows the dog's name is Rex.")]
    public void LeadConversion_MergeKeepsOneCopyOfTheSameNote(string first, string second, string expected)
        => Assert.Equal(expected, AIContactMemoryLeadConversionRepointer.Merge(first, second));

    [Fact]
    public void LeadConversion_MergeKeepsWithinTheLongestNote()
        => Assert.Equal(AIContactMemory.MaxLength, AIContactMemoryLeadConversionRepointer.Merge(new string('a', 1500), new string('b', 1500)).Length);

    private static AIContactMemoryLeadConversionRepointer NewRepointer(IAIContactMemoryStore store)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new AIContactMemoryLeadConversionRepointer(store, clock.Object, NullLogger<AIContactMemoryLeadConversionRepointer>.Instance);
    }

    private static LeadConversionContext NewConversion(string leadId, string contactId)
        => new()
        {
            Lead = new ContentItem { ContentItemId = leadId },
            Contact = new ContentItem { ContentItemId = contactId },
        };

    private sealed class InMemoryMemoryStore : IAIContactMemoryStore
    {
        public List<AIContactMemory> Items { get; } = [];

        public Task<AIContactMemory> FindByContactAsync(string contactId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(memory => memory.ContactId == contactId));

        public Task SaveAsync(AIContactMemory memory, CancellationToken cancellationToken = default)
        {
            if (!Items.Contains(memory))
            {
                Items.Add(memory);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(AIContactMemory memory, CancellationToken cancellationToken = default)
        {
            Items.Remove(memory);

            return Task.CompletedTask;
        }
    }
}
