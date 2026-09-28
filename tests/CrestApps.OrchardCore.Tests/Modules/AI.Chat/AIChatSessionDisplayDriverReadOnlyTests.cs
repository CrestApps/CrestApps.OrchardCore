using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Drivers;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// Bug: the admin "Review AI conversation" page asked the chat hub to load an automated SMS or voice session, which
/// has no user, so the hub's user-scoped lookup missed it and every review ended with a "Session not found." bubble.
/// A stored session with no user is now rendered as a read-only transcript. Confirmed live; these pin the flag the
/// view keys off.
/// </summary>
public sealed class AIChatSessionDisplayDriverReadOnlyTests
{
    [Fact]
    public async Task EditAsync_AStoredSessionWithNoUser_IsReadOnlyInBothShapes()
    {
        // Arrange
        var session = new AIChatSession { SessionId = "session-1", ProfileId = "profile-1", UserId = null };

        // Act
        var models = await BuildModelsAsync(session, isNew: false);

        // Assert
        Assert.Equal(2, models.Count);
        Assert.All(models, model =>
        {
            Assert.True(model.IsReadOnly);
            Assert.False(model.IsNew);
            Assert.Same(session, model.Session);
        });
    }

    [Fact]
    public async Task EditAsync_TheUsersOwnStoredSession_CanBeContinued()
    {
        // Arrange
        var session = new AIChatSession { SessionId = "session-1", ProfileId = "profile-1", UserId = "user-1" };

        // Act
        var models = await BuildModelsAsync(session, isNew: false);

        // Assert
        Assert.Equal(2, models.Count);
        Assert.All(models, model => Assert.False(model.IsReadOnly));
    }

    // A new chat has no user on it until the first message is stored; it must never open read-only.
    [Fact]
    public async Task EditAsync_ANewSessionWithoutAUser_CanBeContinued()
    {
        // Arrange
        var session = new AIChatSession { ProfileId = "profile-1", UserId = null };

        // Act
        var models = await BuildModelsAsync(session, isNew: true);

        // Assert
        Assert.Equal(2, models.Count);
        Assert.All(models, model =>
        {
            Assert.False(model.IsReadOnly);
            Assert.True(model.IsNew);
        });
    }

    private static async Task<List<ChatSessionCapsuleViewModel>> BuildModelsAsync(AIChatSession session, bool isNew)
    {
        var profileManager = new Mock<IAIProfileManager>();
        profileManager
            .Setup(manager => manager.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProfile { ItemId = "profile-1" });

        var driver = new AIChatSessionDisplayDriver(profileManager.Object);
        var context = new BuildEditorContext(
            Mock.Of<IShape>(),
            groupId: string.Empty,
            isNew,
            htmlFieldPrefix: string.Empty,
            Mock.Of<IShapeFactory>(),
            layout: null,
            new PostedFormUpdateModel(null));

        var result = await driver.EditAsync(session, context);

        return await DisplayResultModels.BuildAsync<ChatSessionCapsuleViewModel>(result);
    }
}
