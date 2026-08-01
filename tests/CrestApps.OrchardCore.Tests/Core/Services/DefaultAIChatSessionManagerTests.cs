using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Security;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class DefaultAIChatSessionManagerTests
{
    [Fact]
    public async Task NewAsync_ForAnonymousVisitors_UsesStableVisitorIdentityWithoutSavingInitialPrompt()
    {
        var clock = new Mock<IClock>();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()),
            },
        };

        var visitorIdentityResolver = new Mock<IAIVisitorIdentityResolver>();
        visitorIdentityResolver.Setup(x => x.Resolve()).Returns(new AIVisitorIdentity
        {
            VisitorId = "visitor-123",
            RemoteAddress = "encrypted-ip",
            RemoteAddressHash = "hash-123",
        });

        var promptStore = new Mock<IAIChatSessionPromptStore>();
        var profile = new AIProfile
        {
            ItemId = "profile-1",
            Type = AIProfileType.Chat,
        };
        profile.GetOrCreate<AIProfileMetadata>().InitialPrompt = "hello";

        var manager = new DefaultAIChatSessionManager(
            clock.Object,
            httpContextAccessor,
            visitorIdentityResolver.Object,
            new Mock<YSession>().Object,
            promptStore.Object,
            [],
            Options.Create(new YesSqlStoreOptions()),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DefaultAIChatSessionManager>>());

        var session = await manager.NewAsync(profile, new NewAIChatSessionContext(), TestContext.Current.CancellationToken);

        Assert.Equal("visitor-123", session.ClientId);
        Assert.Equal("encrypted-ip", session.RemoteAddress);
        Assert.Equal("hash-123", session.RemoteAddressHash);
        promptStore.Verify(x => x.CreateAsync(It.IsAny<AIChatSessionPrompt>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
