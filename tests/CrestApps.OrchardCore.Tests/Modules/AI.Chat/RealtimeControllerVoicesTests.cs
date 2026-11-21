using System.Security.Claims;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Speech;
using CrestApps.OrchardCore.AI.Chat.Controllers;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// SEC-A20 c: the realtime voice list is shown on the chat interaction page too, whose users hold the chat interaction
/// permissions and not necessarily the AI profile ones. Either grants the list; neither is refused.
/// </summary>
public sealed class RealtimeControllerVoicesTests
{
    [Fact]
    public async Task Voices_ForAChatInteractionUser_IsNotForbidden()
    {
        var result = await CreateController(AIPermissions.EditOwnChatInteractions).Voices("missing");

        Assert.IsType<JsonResult>(result);
    }

    [Fact]
    public async Task Voices_ForAnAIProfileUser_IsNotForbidden()
    {
        var result = await CreateController(AIPermissions.QueryAnyAIProfile).Voices("missing");

        Assert.IsType<JsonResult>(result);
    }

    [Fact]
    public async Task Voices_ForAUserWithNeither_IsForbidden()
    {
        var result = await CreateController(granted: null).Voices("missing");

        Assert.IsType<ForbidResult>(result);
    }

    private static RealtimeController CreateController(Permission granted)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                granted is not null && requirements.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == granted.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return new RealtimeController(
            authorization.Object,
            Mock.Of<IAIDeploymentManager>(),
            Mock.Of<IAIDeploymentCapabilityService>(),
            Mock.Of<IRealtimeVoiceResolver>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")) },
            },
        };
    }
}
