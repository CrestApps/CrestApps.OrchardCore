using System.Security.Claims;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Memory.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Memory;

public sealed class UserMemoryControllerTests
{
    [Fact]
    public async Task Clear_WhenUserHasMemories_ShouldDeleteAllCurrentUserMemories()
    {
        // Arrange
        const string userId = "user-1";
        const string returnUrl = "/Admin/Users/Edit/user-1";

        var deletedMemories = new List<AIMemoryEntry>();
        var memoryManager = new Mock<ICatalogManager<AIMemoryEntry>>();
        memoryManager
            .Setup(manager => manager.DeleteAsync(It.IsAny<AIMemoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns<AIMemoryEntry, CancellationToken>((memory, _) =>
            {
                deletedMemories.Add(memory);
                return ValueTask.FromResult(true);
            });

        var memoryStore = new Mock<IAIMemoryStore>();
        memoryStore
            .Setup(store => store.CountByUserAsync(userId))
            .ReturnsAsync(2);
        memoryStore
            .Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AIMemoryEntry { ItemId = "memory-1", UserId = userId },
                new AIMemoryEntry { ItemId = "memory-2", UserId = userId },
                new AIMemoryEntry { ItemId = "memory-3", UserId = "another-user" },
            ]);

        var notifier = CreateNotifier();
        var controller = CreateController(memoryManager.Object, memoryStore.Object, notifier.Object, userId);

        // Act
        var result = await controller.Clear(userId, returnUrl);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(returnUrl, redirectResult.Url);
        Assert.Equal(["memory-1", "memory-2"], deletedMemories.Select(memory => memory.ItemId));
        memoryStore.Verify(store => store.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(
            service => service.AddAsync(NotifyType.Success, It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>()),
            Times.Once);
    }

    [Fact]
    public async Task Clear_WhenUserHasNoMemories_ShouldNotDeleteAnything()
    {
        // Arrange
        const string userId = "user-1";
        const string returnUrl = "/Admin/Users/Edit/user-1";

        var memoryManager = new Mock<ICatalogManager<AIMemoryEntry>>();
        var memoryStore = new Mock<IAIMemoryStore>();
        memoryStore
            .Setup(store => store.CountByUserAsync(userId))
            .ReturnsAsync(0);

        var notifier = CreateNotifier();
        var controller = CreateController(memoryManager.Object, memoryStore.Object, notifier.Object, userId);

        // Act
        var result = await controller.Clear(userId, returnUrl);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(returnUrl, redirectResult.Url);
        memoryStore.Verify(store => store.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        memoryManager.Verify(manager => manager.DeleteAsync(It.IsAny<AIMemoryEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(
            service => service.AddAsync(NotifyType.Warning, It.IsAny<LocalizedHtmlString>()),
            Times.Once);
    }

    [Fact]
    public async Task Clear_WhenStoreReturnsNoCurrentUserMemories_ShouldWarnWithoutDeleting()
    {
        // Arrange
        const string userId = "user-1";
        const string returnUrl = "/Admin/Users/Edit/user-1";

        var memoryManager = new Mock<ICatalogManager<AIMemoryEntry>>();
        var memoryStore = new Mock<IAIMemoryStore>();
        memoryStore
            .Setup(store => store.CountByUserAsync(userId))
            .ReturnsAsync(2);
        memoryStore
            .Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AIMemoryEntry { ItemId = "memory-3", UserId = "another-user" },
            ]);

        var notifier = CreateNotifier();
        var controller = CreateController(memoryManager.Object, memoryStore.Object, notifier.Object, userId);

        // Act
        var result = await controller.Clear(userId, returnUrl);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(returnUrl, redirectResult.Url);
        memoryManager.Verify(manager => manager.DeleteAsync(It.IsAny<AIMemoryEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(
            service => service.AddAsync(NotifyType.Warning, It.IsAny<LocalizedHtmlString>()),
            Times.Once);
    }

    private static Mock<INotifier> CreateNotifier()
    {
        var notifier = new Mock<INotifier>();
        notifier
            .Setup(service => service.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
            .Returns(ValueTask.CompletedTask);
        notifier
            .Setup(service => service.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>()))
            .Returns(ValueTask.CompletedTask);

        return notifier;
    }

    private static UserMemoryController CreateController(
        ICatalogManager<AIMemoryEntry> memoryManager,
        IAIMemoryStore memoryStore,
        INotifier notifier,
        string currentUserId)
    {
        var controller = new UserMemoryController(
            memoryManager,
            memoryStore,
            notifier,
            Mock.Of<IHtmlLocalizer<UserMemoryController>>(),
            Options.Create(new AdminOptions()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, currentUserId),
                    ], "Test")),
                },
            },
        };

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper
            .Setup(helper => helper.IsLocalUrl(It.IsAny<string>()))
            .Returns<string>(url => !string.IsNullOrWhiteSpace(url) && url.StartsWith('/'));
        urlHelper
            .Setup(helper => helper.Content(It.IsAny<string>()))
            .Returns<string>(url => url);

        controller.Url = urlHelper.Object;

        return controller;
    }
}
