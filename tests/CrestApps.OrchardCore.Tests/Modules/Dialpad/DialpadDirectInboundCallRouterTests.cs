using System.Linq.Expressions;
using CrestApps.OrchardCore.Dialpad;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Dialpad;

public sealed class DialpadDirectInboundCallRouterTests
{
    [Fact]
    public async Task RouteAsync_WhenTargetUserIdMatchesConnectedUser_DispatchesToResolvedUser()
    {
        // Arrange
        var dispatcher = new Mock<IIncomingCallDispatcher>();
        var session = CreateSession([
            new TelephonyUserConnectionIndex
            {
                UserId = "user-1",
                ProviderName = DialpadConstants.ProviderTechnicalName,
                RemoteUserId = "5171365938069504",
                IsEnabled = true,
            },
        ]);
        var lookupNormalizer = CreateLookupNormalizer();
        var router = new DialpadDirectInboundCallRouter(
            session.Object,
            lookupNormalizer.Object,
            dispatcher.Object,
            NullLogger<DialpadDirectInboundCallRouter>.Instance);
        var callEvent = new DialpadCallEvent
        {
            CallId = "call-1",
            State = "ringing",
            Direction = "inbound",
            ExternalNumber = "+17024993350",
            TargetId = "5171365938069504",
            TargetType = "user",
            TargetPhone = "+12088208280",
            TargetName = "Agent One",
        };

        // Act
        var routed = await router.RouteAsync(
            callEvent,
            new DateTime(2026, 8, 19, 16, 0, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(routed);
        dispatcher.Verify(
            value => value.DispatchAsync(
                "user-1",
                It.Is<TelephonyCall>(call =>
                    call.CallId == "call-1" &&
                    call.Direction == CallDirection.Inbound &&
                    call.State == CallState.Ringing &&
                    call.From == "+17024993350"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RouteAsync_WhenServiceAddressMatchesConnectedPhone_DispatchesToResolvedUser()
    {
        // Arrange
        var dispatcher = new Mock<IIncomingCallDispatcher>();
        var session = CreateSession([
            new TelephonyUserConnectionIndex
            {
                UserId = "user-2",
                ProviderName = DialpadConstants.ProviderTechnicalName,
                NormalizedRemotePhoneNumber = "+12088208280",
                IsEnabled = true,
            },
        ]);
        var lookupNormalizer = CreateLookupNormalizer();
        var router = new DialpadDirectInboundCallRouter(
            session.Object,
            lookupNormalizer.Object,
            dispatcher.Object,
            NullLogger<DialpadDirectInboundCallRouter>.Instance);
        var callEvent = new DialpadCallEvent
        {
            CallId = "call-2",
            State = "connected",
            Direction = "inbound",
            ExternalNumber = "+17024993350",
            InternalNumber = "+1 (208) 820-8280",
        };

        // Act
        var routed = await router.RouteAsync(
            callEvent,
            new DateTime(2026, 8, 19, 16, 5, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(routed);
        dispatcher.Verify(
            value => value.DispatchAsync(
                "user-2",
                It.Is<TelephonyCall>(call =>
                    call.CallId == "call-2" &&
                    call.State == CallState.Connected &&
                    call.To == "+1 (208) 820-8280"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RouteAsync_WhenMultipleUsersMatchSameTarget_DoesNotMisroute()
    {
        // Arrange
        var dispatcher = new Mock<IIncomingCallDispatcher>();
        var session = CreateSession([
            new TelephonyUserConnectionIndex
            {
                UserId = "user-1",
                ProviderName = DialpadConstants.ProviderTechnicalName,
                RemoteUserId = "5171365938069504",
                IsEnabled = true,
            },
            new TelephonyUserConnectionIndex
            {
                UserId = "user-2",
                ProviderName = DialpadConstants.ProviderTechnicalName,
                RemoteUserId = "5171365938069504",
                IsEnabled = true,
            },
        ]);
        var lookupNormalizer = CreateLookupNormalizer();
        var router = new DialpadDirectInboundCallRouter(
            session.Object,
            lookupNormalizer.Object,
            dispatcher.Object,
            NullLogger<DialpadDirectInboundCallRouter>.Instance);
        var callEvent = new DialpadCallEvent
        {
            CallId = "call-3",
            State = "ringing",
            Direction = "inbound",
            ExternalNumber = "+17024993350",
            TargetId = "5171365938069504",
        };

        // Act
        var routed = await router.RouteAsync(
            callEvent,
            new DateTime(2026, 8, 19, 16, 10, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(routed);
        dispatcher.Verify(
            value => value.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<TelephonyCall>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<ISession> CreateSession(IReadOnlyCollection<TelephonyUserConnectionIndex> rows)
    {
        Expression<Func<TelephonyUserConnectionIndex, bool>> predicate = null;
        var indexQuery = new Mock<IQueryIndex<TelephonyUserConnectionIndex>>();
        indexQuery
            .Setup(query => query.Where(It.IsAny<Expression<Func<TelephonyUserConnectionIndex, bool>>>()))
            .Callback<Expression<Func<TelephonyUserConnectionIndex, bool>>>(value => predicate = value)
            .Returns(indexQuery.Object);
        indexQuery
            .Setup(query => query.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var compiled = predicate?.Compile() ?? (_ => true);

                return rows.Where(compiled).ToList();
            });

        var query = new Mock<IQuery>();
        query
            .Setup(value => value.ForIndex<TelephonyUserConnectionIndex>())
            .Returns(indexQuery.Object);

        var session = new Mock<ISession>();
        session
            .Setup(value => value.Query(null))
            .Returns(query.Object);

        return session;
    }

    private static Mock<ILookupNormalizer> CreateLookupNormalizer()
    {
        var normalizer = new Mock<ILookupNormalizer>();
        normalizer
            .Setup(value => value.NormalizeEmail(It.IsAny<string>()))
            .Returns<string>(value => value?.Trim().ToUpperInvariant());

        return normalizer;
    }
}
