using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
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

public sealed class AIProfileVersionCompareAndLeadMemoryTests
{
    private static readonly DateTime _now = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Compare_ListsEverySettingGroupedLikeTheEditor()
    {
        var then = NewProfile();
        var now = NewProfile();
        now.AlterSettings<AIProfileGuardrailSettings>(settings =>
        {
            settings.Topics = [AIGuardrailTopics.Violence, AIGuardrailTopics.Hate];
            settings.PlaceholderReply = "Let's keep to your order.";
        });
        now.AlterSettings<AIProfileSpeakingStyleSettings>(settings => settings.Styles = [AISpeakingStyles.AIDisclosure]);
        now.Put(new AIProfileMetadata { SystemMessage = "Be brief.", Temperature = 0.2f });

        var titles = new Dictionary<string, string>
        {
            [AIGuardrailTopics.Violence] = "Violence and weapons",
            [AISpeakingStyles.AIDisclosure] = "AI disclosure",
        };

        var rows = AIProfileVersionComparer.Compare(then, now, titles);

        // Unchanged settings are listed too, so a compare shows the whole profile.
        var title = Assert.Single(rows, row => row.Label == "Title");
        Assert.Equal(AIProfileVersionComparer.General, title.Section);
        Assert.False(title.Changed);

        var memory = Assert.Single(rows, row => row.Label == "Contact Memory › Use memory");
        Assert.Equal(AIProfileVersionComparer.DataProcessing, memory.Section);
        Assert.Equal("Yes", memory.Then);
        Assert.False(memory.Changed);

        var topics = Assert.Single(rows, row => row.Label == "Guardrail › Topics");
        Assert.Equal(AIProfileVersionComparer.PromptSecurity, topics.Section);
        Assert.True(topics.Changed);
        Assert.Null(topics.Then);
        Assert.Equal("Violence and weapons (guardrail-topic-violence), guardrail-topic-hate", topics.Now);

        var styles = Assert.Single(rows, row => row.Label == "Speaking Style › Styles");
        Assert.Equal(AIProfileVersionComparer.Instructions, styles.Section);
        Assert.Equal("AI disclosure (speaking-style-ai-disclosure)", styles.Now);

        var systemMessage = Assert.Single(rows, row => row.Label == "System message");
        Assert.Equal(AIProfileVersionComparer.Instructions, systemMessage.Section);
        Assert.Equal("Be careful.", systemMessage.Then);
        Assert.Equal("Be brief.", systemMessage.Now);

        Assert.Equal(AIProfileVersionComparer.Parameters, Assert.Single(rows, row => row.Label == "Temperature").Section);
        Assert.Equal(AIProfileVersionComparer.Capabilities, Assert.Single(rows, row => row.Label == "AI Tool Instance › Tool instance names").Section);

        // What identifies the profile, or changes on every save, is left out.
        Assert.DoesNotContain(rows, row => row.Label is "Item id" or "Owner id" or "Created utc" || row.Label.StartsWith("Version", StringComparison.Ordinal));

        // Rows follow the editor's tabs.
        var order = rows.Select(row => Array.IndexOf(AIProfileVersionComparer.Sections, row.Section)).ToList();
        Assert.Equal(order.OrderBy(index => index), order);
    }

    [Theory]
    [InlineData("AIProfileGuardrailSettings", "Guardrail")]
    [InlineData("FunctionInvocationMetadata", "Function Invocation")]
    [InlineData("PromptSecurityProfileSettings", "Prompt Security")]
    [InlineData("AIProfileDataExtractionSettings", "Data Extraction")]
    public void Compare_NamesGroupsAfterTheirSettings(string key, string expected)
        => Assert.Equal(expected, AIProfileVersionComparer.GroupOf(key));

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

    private static AIProfile NewProfile()
    {
        var profile = new AIProfile
        {
            ItemId = "p1",
            Name = "support",
            DisplayText = "Support",
            OwnerId = "u1",
            ChatDeploymentName = "gpt-4.1-mini",
        };

        profile.Put(new AIProfileMetadata { SystemMessage = "Be careful.", Temperature = 0.7f });
        profile.Put(new AIProfileVersionMetadata { Version = 3 });
        profile.Put(new AIToolInstanceMetadata { ToolInstanceNames = ["OrderStatus"] });
        profile.AlterSettings<AIProfileContactMemorySettings>(settings => settings.UseMemory = true);

        return profile;
    }

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
