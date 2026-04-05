using System.Security.Claims;
using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.AI.ResponseHandling;
using CrestApps.Mvc.Web.Areas.AIChat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Moq;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class YesSqlAIChatSessionManagerTests
{
    [Fact]
    public async Task NewAsync_WithInitialPrompt_ShouldCreateGeneratedAssistantPrompt()
    {
        var promptStore = new Mock<IAIChatSessionPromptStore>();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
            ], "Test")),
        });

        var profile = new AIProfile
        {
            ItemId = "profile-1",
            Type = AIProfileType.Chat,
            PromptSubject = "Welcome",
        };
        profile.Put(new AIProfileMetadata
        {
            InitialPrompt = "Hello there",
        });
        profile.AlterSettings<ResponseHandlerProfileSettings>(settings =>
        {
            settings.InitialResponseHandlerName = "handoff";
        });

        var manager = new YesSqlAIChatSessionManager(
            httpContextAccessor.Object,
            new Mock<global::YesSql.ISession>().Object,
            promptStore.Object,
            TimeProvider.System);

        var session = await manager.NewAsync(profile, new NewAIChatSessionContext());

        Assert.Equal("user-1", session.UserId);
        Assert.Equal("handoff", session.ResponseHandlerName);
        promptStore.Verify(store => store.CreateAsync(It.Is<AIChatSessionPrompt>(prompt =>
            prompt.SessionId == session.SessionId &&
            prompt.Role == ChatRole.Assistant &&
            prompt.Title == "Welcome" &&
            prompt.Content == "Hello there" &&
            prompt.IsGeneratedPrompt)),
            Times.Once);
    }
}
