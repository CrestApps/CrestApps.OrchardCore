using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Controllers;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        var controller = new ChatConversionGoalsController(
            profileManager.Object,
            new Mock<YSession>().Object,
            authorizationService.Object,
            new Mock<IClock>().Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")),
            },
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
