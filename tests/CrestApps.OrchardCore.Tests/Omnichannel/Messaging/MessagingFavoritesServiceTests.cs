using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// An agent stars the customers they message most so they are one click away. The list is the agent's own and must
/// keep recognising a customer as their conversation changes around them.
/// </summary>
public sealed class MessagingFavoritesServiceTests
{
    [Fact]
    public async Task SetFavoriteAsync_StarsTheCustomer_AndSavesItOnTheAgentProfile()
    {
        var (service, manager) = CreateService();
        var agent = new AgentProfile { ItemId = "agent-1" };

        var changed = await service.SetFavoriteAsync(agent, Conversation(contactId: "contact-1"), "Jane Doe", favorite: true, TestContext.Current.CancellationToken);

        Assert.True(changed);

        var favorite = Assert.Single(service.GetFavorites(agent));

        Assert.Equal("contact:contact-1", favorite.CustomerKey);
        Assert.Equal("Jane Doe", favorite.DisplayName);
        Assert.Equal("+15551112222", favorite.ContactAddress);
        manager.Verify(m => m.UpdateAsync(agent, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetFavoriteAsync_StarringTwice_KeepsOneEntry()
    {
        var (service, _) = CreateService();
        var agent = new AgentProfile { ItemId = "agent-1" };

        await service.SetFavoriteAsync(agent, Conversation(contactId: "contact-1"), "Jane", favorite: true, TestContext.Current.CancellationToken);
        var changed = await service.SetFavoriteAsync(agent, Conversation(contactId: "contact-1"), "Jane", favorite: true, TestContext.Current.CancellationToken);

        Assert.False(changed);
        Assert.Single(service.GetFavorites(agent));
    }

    [Fact]
    public async Task IsFavorite_StillRecognisesAnUnknownSender_AfterAContactIsLinkedToTheirNumber()
    {
        // Starred as a bare number, the customer's key later becomes their contact. The address still ties them together.
        var (service, _) = CreateService();
        var agent = new AgentProfile { ItemId = "agent-1" };

        await service.SetFavoriteAsync(agent, Conversation(contactId: null), null, favorite: true, TestContext.Current.CancellationToken);

        Assert.True(service.IsFavorite(agent, Conversation(contactId: "contact-9")));
    }

    [Fact]
    public async Task SetFavoriteAsync_Unstarring_RemovesTheCustomer()
    {
        var (service, _) = CreateService();
        var agent = new AgentProfile { ItemId = "agent-1" };

        await service.SetFavoriteAsync(agent, Conversation(contactId: "contact-1"), "Jane", favorite: true, TestContext.Current.CancellationToken);
        var changed = await service.SetFavoriteAsync(agent, Conversation(contactId: "contact-1"), "Jane", favorite: false, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Empty(service.GetFavorites(agent));
        Assert.False(service.IsFavorite(agent, Conversation(contactId: "contact-1")));
    }

    [Fact]
    public async Task SetFavoriteAsync_KeepsTheListBounded_DroppingTheOldestStar()
    {
        var (service, _) = CreateService();
        var agent = new AgentProfile { ItemId = "agent-1" };

        for (var index = 0; index <= MessagingFavorites.MaxFavorites; index++)
        {
            await service.SetFavoriteAsync(agent, Conversation($"contact-{index}", $"+1555000{index:0000}"), null, favorite: true, TestContext.Current.CancellationToken);
        }

        var favorites = service.GetFavorites(agent);

        Assert.Equal(MessagingFavorites.MaxFavorites, favorites.Count);
        Assert.Equal($"contact:contact-{MessagingFavorites.MaxFavorites}", favorites[0].CustomerKey);
        Assert.DoesNotContain(favorites, favorite => favorite.CustomerKey == "contact:contact-0");
    }

    [Fact]
    public void GetFavorites_ForSomeoneWithoutAnAgentProfile_IsEmpty()
    {
        var (service, _) = CreateService();

        Assert.Empty(service.GetFavorites(null));
        Assert.False(service.IsFavorite(null, Conversation(contactId: "contact-1")));
    }

    private static MessagingConversation Conversation(string contactId, string address = "+15551112222")
        => new()
        {
            ItemId = "conv-" + (contactId ?? address),
            Channel = "SMS",
            ServiceAddress = "+15553334444",
            ContactAddress = address,
            ContactContentItemId = contactId,
        };

    private static (MessagingFavoritesService Service, Mock<IAgentProfileManager> Manager) CreateService()
    {
        var manager = new Mock<IAgentProfileManager>();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));

        return (new MessagingFavoritesService(manager.Object, clock.Object), manager);
    }
}
