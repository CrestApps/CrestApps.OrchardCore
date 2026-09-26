using System.Linq.Expressions;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Security;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;
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
            Mock.Of<IAIChatSessionStore>(),
            [],
            Options.Create(new YesSqlStoreOptions()),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DefaultAIChatSessionManager>>());

        var session = await manager.NewAsync(profile, new NewAIChatSessionContext(), TestContext.Current.CancellationToken);

        Assert.Equal("visitor-123", session.ClientId);
        Assert.Equal("encrypted-ip", session.RemoteAddress);
        Assert.Equal("hash-123", session.RemoteAddressHash);
        promptStore.Verify(x => x.CreateAsync(It.IsAny<AIChatSessionPrompt>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PageAsync_ForAnonymousVisitor_ReturnsOnlyThatVisitorsSessions()
    {
        var sessions = new[]
        {
            new AIChatSession { SessionId = "mine", ProfileId = "profile-1", ClientId = "visitor-123", Title = "Mine" },
            new AIChatSession { SessionId = "theirs", ProfileId = "profile-1", ClientId = "visitor-999", Title = "Theirs" },
        };

        var manager = CreateManager(CreateAnonymousAccessor(), CreateVisitorResolver("visitor-123"), CreateSessionReturning(sessions));

        var result = await manager.PageAsync(1, 20, new AIChatSessionQueryContext
        {
            ProfileId = "profile-1",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Count);
        Assert.Equal("mine", Assert.Single(result.Sessions).SessionId);
    }

    [Fact]
    public async Task PageAsync_ForAnonymousVisitorWithoutAVisitorId_ReturnsNothing()
    {
        var manager = CreateManager(CreateAnonymousAccessor(), CreateVisitorResolver(null), new Mock<YSession>().Object);

        var result = await manager.PageAsync(1, 20, new AIChatSessionQueryContext(), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Count);
        Assert.Empty(result.Sessions);
    }

    private static DefaultAIChatSessionManager CreateManager(
        IHttpContextAccessor httpContextAccessor,
        IAIVisitorIdentityResolver visitorIdentityResolver,
        YSession session)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new DefaultAIChatSessionManager(
            clock.Object,
            httpContextAccessor,
            visitorIdentityResolver,
            session,
            new Mock<IAIChatSessionPromptStore>().Object,
            Mock.Of<IAIChatSessionStore>(),
            [],
            Options.Create(new YesSqlStoreOptions()),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DefaultAIChatSessionManager>>());
    }

    private static HttpContextAccessor CreateAnonymousAccessor()
        => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()),
            },
        };

    private static IAIVisitorIdentityResolver CreateVisitorResolver(string visitorId)
    {
        var resolver = new Mock<IAIVisitorIdentityResolver>();
        resolver.Setup(x => x.Resolve()).Returns(new AIVisitorIdentity
        {
            VisitorId = visitorId,
        });

        return resolver.Object;
    }

    private static YSession CreateSessionReturning(IReadOnlyList<AIChatSession> sessions)
    {
        var indexQuery = new Mock<IQuery<AIChatSession, AIChatSessionIndex>>();
        indexQuery
            .Setup(x => x.Where(It.IsAny<Expression<Func<AIChatSessionIndex, bool>>>()))
            .Returns(() => indexQuery.Object);
        indexQuery
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<IReadOnlyList<AIChatSession>>(sessions));

        var entityQuery = new Mock<IQuery<AIChatSession>>();
        entityQuery
            .Setup(x => x.With(It.IsAny<Expression<Func<AIChatSessionIndex, bool>>>()))
            .Returns(indexQuery.Object);

        var rootQuery = new Mock<IQuery>();
        rootQuery
            .Setup(x => x.For<AIChatSession>(It.IsAny<bool>()))
            .Returns(entityQuery.Object);

        var session = new Mock<YSession>();
        session
            .Setup(x => x.Query(It.IsAny<string>()))
            .Returns(rootQuery.Object);

        return session.Object;
    }
}
