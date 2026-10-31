using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIContactMemoryRebuildTests
{
    [Fact]
    public async Task GetInstructionsAsync_UsesTheTenantsOwnWhenSet()
    {
        // Arrange
        var site = new SiteSettings();
        site.Put(new AIContactMemorySettings { Instructions = "Keep only the customer's open orders." });
        var service = Service(site: site);

        // Act
        var instructions = await service.GetInstructionsAsync();

        // Assert
        Assert.StartsWith("Keep only the customer's open orders.", instructions, StringComparison.Ordinal);
        Assert.Contains(AIContactMemory.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture), instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetInstructionsAsync_FallsBackToTheBuiltInOnes()
        => Assert.Equal(AIContactMemoryService.SystemPrompt, await Service(site: new SiteSettings()).GetInstructionsAsync());

    [Fact]
    public async Task RebuildAsync_ClearsTheNoteAndReadsEachConversationOldestFirst()
    {
        // Arrange
        var store = new Mock<IAIContactMemoryStore>();
        var deleted = new List<string>();
        store.Setup(x => x.FindByContactAsync("c-1", It.IsAny<CancellationToken>())).ReturnsAsync(new AIContactMemory { ContactId = "c-1", Text = "old" });
        store.Setup(x => x.DeleteAsync(It.IsAny<AIContactMemory>(), It.IsAny<CancellationToken>()))
            .Callback<AIContactMemory, CancellationToken>((memory, _) => deleted.Add(memory.Text))
            .Returns(Task.CompletedTask);
        var read = new List<string>();
        var prompts = new Mock<IAIChatSessionPromptStore>();
        prompts.Setup(x => x.GetPromptsAsync(It.IsAny<string>()))
            .Callback<string>(read.Add)
            .ReturnsAsync([]);
        var service = Service(store.Object, prompts.Object);

        // Act
        var note = await service.RebuildAsync("c-1", ["s-1", "s-2", "s-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(note);
        Assert.Equal(["s-1", "s-2"], read);
        Assert.Equal(["old"], deleted);
    }

    private static AIContactMemoryService Service(IAIContactMemoryStore store = null, IAIChatSessionPromptStore promptStore = null, ISite site = null)
    {
        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync()).ReturnsAsync(site ?? new SiteSettings());

        return new AIContactMemoryService(
            store ?? Mock.Of<IAIContactMemoryStore>(),
            Mock.Of<IAIDeploymentManager>(),
            Mock.Of<IAIClientFactory>(),
            Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc)),
            siteService.Object,
            promptStore);
    }
}
