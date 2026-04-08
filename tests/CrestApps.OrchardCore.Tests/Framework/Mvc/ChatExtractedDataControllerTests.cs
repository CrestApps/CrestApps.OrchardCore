using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Controllers;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Core.Mvc.Web.Areas.AIChat.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class ChatExtractedDataControllerTests
{
    [Fact]
    public async Task IndexPost_WithoutProfileSelection_ShouldReturnValidationError()
    {
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

        var controller = new ChatExtractedDataController(
            profileManager.Object,
            new MvcAIChatSessionExtractedDataService(new Mock<global::YesSql.ISession>().Object, TimeProvider.System),
            TimeProvider.System);

        var result = await controller.IndexPost(new ChatExtractedDataIndexViewModel());

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ChatExtractedDataIndexViewModel>(viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(ChatExtractedDataIndexViewModel.ProfileId)].Errors, error => error.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, model.Profiles.Count);
    }
}
