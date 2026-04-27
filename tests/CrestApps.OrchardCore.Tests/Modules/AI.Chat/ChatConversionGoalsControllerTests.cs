using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Chat.Controllers;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

public sealed class ChatConversionGoalsControllerTests
{
    [Fact]
    public async Task IndexPost_WithoutProfileSelection_ShouldReturnValidationError()
    {
        // Arrange
        var profileManager = new Mock<IAIProfileManager>();
        profileManager
            .Setup(manager => manager.GetAsync(AIProfileType.Chat))
            .ReturnsAsync(
            [
                new AIProfile
                {
                    ItemId = "profile-1",
                    Name = "support",
                    DisplayText = "Support",
                },
            ]);

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var yesSqlStoreOptions = new Mock<IOptions<YesSqlStoreOptions>>();

        var controller = new ChatConversionGoalsController(
            profileManager.Object,
            new Mock<YSession>().Object,
            authorizationService.Object,
            yesSqlStoreOptions.Object,
            new Mock<IClock>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")),
                },
            }
        };

        // Act
        var result = await controller.IndexPost(new ChatConversionGoalsIndexViewModel());

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ChatConversionGoalsIndexViewModel>(viewResult.Model);

        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[nameof(ChatConversionGoalsIndexViewModel.ProfileId)].Errors,
            error => error.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, model.Profiles.Count);
    }
}
