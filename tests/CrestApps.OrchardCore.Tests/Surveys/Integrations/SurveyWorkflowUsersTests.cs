using CrestApps.OrchardCore.Surveys.Workflows;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.Integrations;

public sealed class SurveyWorkflowUsersTests
{
    [Theory]
    [InlineData("4xyzuserid0000000000000000", "4xyzuserid0000000000000000")]
    [InlineData("learner.with.a.long.user.name", "4xyzuserid0000000000000000")]
    [InlineData("learner@example.com", "4xyzuserid0000000000000000")]
    [InlineData("nobody-with-a-very-long-made-up-name", null)]
    [InlineData("  ", null)]
    public async Task ResolveAsync_FindsTheUserByIdNameOrEmail_AndNothingForAnUnknownValue(string value, string expected)
    {
        // Arrange
        var user = new User { UserId = "4xyzuserid0000000000000000", UserName = "learner.with.a.long.user.name", Email = "learner@example.com" };
        var manager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        manager.Setup(m => m.FindByIdAsync(user.UserId)).ReturnsAsync(user);
        manager.Setup(m => m.FindByNameAsync(user.UserName)).ReturnsAsync(user);
        manager.Setup(m => m.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        manager.Setup(m => m.GetUserIdAsync(user)).ReturnsAsync(user.UserId);
        var services = new ServiceCollection().AddSingleton(manager.Object).BuildServiceProvider();

        // Act
        var resolved = await SurveyWorkflowUsers.ResolveAsync(services, value);

        // Assert
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public async Task ResolveAsync_WithoutUsers_RefusesAValueTooLongForAUserId()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();

        // Act + Assert
        Assert.Null(await SurveyWorkflowUsers.ResolveAsync(services, new string('x', SurveyWorkflowUsers.MaxUserIdLength + 1)));
        Assert.Equal("short-id", await SurveyWorkflowUsers.ResolveAsync(services, "short-id"));
    }
}
