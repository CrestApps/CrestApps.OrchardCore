using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Handlers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Identity;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Validation;

/// <summary>
/// Pins the extension rules to the handler, so a recipe cannot store an extension the editor would have refused, and
/// pins how an imported extension finds the user it rings.
/// </summary>
public sealed class TelephonyExtensionHandlerValidationTests
{
    [Fact]
    public async Task ValidatingAsync_WhenTheNumberAndUserAreValid_Succeeds()
    {
        // Arrange
        var extension = new TelephonyExtension { ItemId = "ext-1", Number = "1001", UserId = "user-1" };

        // Act
        var context = await ValidateAsync(extension);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidatingAsync_WhenTheNumberIsMissing_Fails(string number)
    {
        // Arrange
        var extension = new TelephonyExtension { ItemId = "ext-1", Number = number, UserId = "user-1" };

        // Act
        var context = await ValidateAsync(extension);

        // Assert
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(TelephonyExtension.Number)));
    }

    [Fact]
    public async Task ValidatingAsync_WhenAnotherExtensionHoldsTheNumber_Fails()
    {
        // Arrange
        var extension = new TelephonyExtension { ItemId = "ext-1", Number = "1001", UserId = "user-1" };
        var holder = new TelephonyExtension { ItemId = "ext-2", Number = "1001", UserId = "user-2" };

        // Act
        var context = await ValidateAsync(extension, holder);

        // Assert
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(TelephonyExtension.Number)));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheExtensionHoldsItsOwnNumber_Succeeds()
    {
        // Arrange
        var extension = new TelephonyExtension { ItemId = "ext-1", Number = "1001", UserId = "user-1" };

        // Act
        var context = await ValidateAsync(extension, extension);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing-user")]
    public async Task ValidatingAsync_WhenTheUserDoesNotExist_Fails(string userId)
    {
        // Arrange
        var extension = new TelephonyExtension { ItemId = "ext-1", Number = "1001", UserId = userId };

        // Act
        var context = await ValidateAsync(extension);

        // Assert
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(TelephonyExtension.UserId)));
    }

    [Fact]
    public async Task InitializingAsync_WhenImportedDataNamesAUser_StoresThatUsersLocalIdentifier()
    {
        // Arrange
        // The plan was exported from another environment, where the same person has a different identifier.
        var extension = new TelephonyExtension();
        var data = new JsonObject
        {
            [nameof(TelephonyExtension.Number)] = " 1001 ",
            [nameof(TelephonyExtension.UserName)] = "agent",
            [nameof(TelephonyExtension.UserId)] = "identifier-from-another-environment",
        };

        // Act
        await CreateHandler().InitializingAsync(new InitializingContext<TelephonyExtension>(extension, data), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("1001", extension.Number);
        Assert.Equal("user-1", extension.UserId);
        Assert.Equal("agent", extension.UserName);
        Assert.Equal("agent", extension.DisplayName);
        Assert.Equal("1001 agent", extension.Name);
    }

    [Fact]
    public async Task InitializingAsync_WhenImportedDataNamesAnUnknownUser_ClearsTheIdentifierSoValidationFails()
    {
        // Arrange
        var extension = new TelephonyExtension();
        var data = new JsonObject
        {
            [nameof(TelephonyExtension.Number)] = "1001",
            [nameof(TelephonyExtension.UserName)] = "nobody",
            [nameof(TelephonyExtension.UserId)] = "user-1",
        };
        var handler = CreateHandler();

        // Act
        await handler.InitializingAsync(new InitializingContext<TelephonyExtension>(extension, data), TestContext.Current.CancellationToken);
        var context = new ValidatingContext<TelephonyExtension>(extension);
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        // Keeping the foreign identifier could ring whoever holds it on this tenant, so the entry is refused instead.
        Assert.Null(extension.UserId);
        Assert.False(context.Result.Succeeded);
    }

    private static async Task<ValidatingContext<TelephonyExtension>> ValidateAsync(TelephonyExtension extension, TelephonyExtension numberHolder = null)
    {
        var context = new ValidatingContext<TelephonyExtension>(extension);

        await CreateHandler(numberHolder).ValidatingAsync(context, TestContext.Current.CancellationToken);

        return context;
    }

    private static TelephonyExtensionHandler CreateHandler(TelephonyExtension numberHolder = null)
    {
        var store = new Mock<ITelephonyExtensionStore>();
        store.Setup(candidate => candidate.FindByNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string number, CancellationToken _) => numberHolder is not null && numberHolder.Number == number ? numberHolder : null);

        var user = Mock.Of<IUser>();
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByIdAsync("user-1")).ReturnsAsync(user);
        userManager.Setup(manager => manager.FindByNameAsync("agent")).ReturnsAsync(user);
        userManager.Setup(manager => manager.GetUserIdAsync(user)).ReturnsAsync("user-1");
        userManager.Setup(manager => manager.GetUserNameAsync(user)).ReturnsAsync("agent");

        return new TelephonyExtensionHandler(
            store.Object,
            userManager.Object,
            new Mock<IClock>().Object,
            new PassThroughStringLocalizer<TelephonyExtensionHandler>());
    }
}
