using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Proves the telephony extension manager resolves extensions by number and by user through its store, returning
/// the persisted entry when one exists and null when none does, so extension dialing and per-user routing look up
/// the same durable record the store owns.
/// </summary>
public sealed class TelephonyExtensionManagerTests
{
    [Fact]
    public async Task FindByNumberAsync_WhenTheStoreHasTheExtension_ReturnsIt()
    {
        // Arrange
        var extension = new TelephonyExtension { ItemId = "ext-1", Number = "1001", Name = "Reception" };
        var store = CreateStore();
        store.Setup(x => x.FindByNumberAsync("1001", It.IsAny<CancellationToken>())).ReturnsAsync(extension);
        var manager = CreateManager(store);

        // Act
        var found = await manager.FindByNumberAsync("1001", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(extension, found);
        store.Verify(x => x.FindByNumberAsync("1001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindByNumberAsync_WhenTheStoreHasNoExtension_ReturnsNull()
    {
        // Arrange
        var store = CreateStore();
        store.Setup(x => x.FindByNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((TelephonyExtension)null);
        var manager = CreateManager(store);

        // Act
        var found = await manager.FindByNumberAsync("9999", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public async Task FindByUserIdAsync_WhenTheStoreHasTheExtension_ReturnsIt()
    {
        // Arrange
        var extension = new TelephonyExtension { ItemId = "ext-2", Number = "1002", UserId = "user-2" };
        var store = CreateStore();
        store.Setup(x => x.FindByUserIdAsync("user-2", It.IsAny<CancellationToken>())).ReturnsAsync(extension);
        var manager = CreateManager(store);

        // Act
        var found = await manager.FindByUserIdAsync("user-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(extension, found);
        store.Verify(x => x.FindByUserIdAsync("user-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindByUserIdAsync_WhenTheStoreHasNoExtension_ReturnsNull()
    {
        // Arrange
        var store = CreateStore();
        store.Setup(x => x.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((TelephonyExtension)null);
        var manager = CreateManager(store);

        // Act
        var found = await manager.FindByUserIdAsync("nobody", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(found);
    }

    private static Mock<ITelephonyExtensionStore> CreateStore()
        => new();

    private static TelephonyExtensionManager CreateManager(Mock<ITelephonyExtensionStore> store)
        => new(
            store.Object,
            [],
            NullLogger<CatalogManager<TelephonyExtension>>.Instance);
}
