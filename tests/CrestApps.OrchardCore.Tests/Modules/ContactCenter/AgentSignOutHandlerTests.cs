using CrestApps.Core.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using CrestApps.Core.Telephony;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins what ending a session does to an agent.
/// </summary>
/// <remarks>
/// This runs inside the host's sign-out pipeline, before the authentication cookie is deleted. Two
/// properties matter more than the happy path: it must never throw, because an exception escaping
/// would abort the sign-out and leave the user logged in; and it must revoke soft-phone credentials,
/// because an agent whose browser keeps live SIP credentials after logging off is a security problem
/// rather than a stale-state one.
/// </remarks>
public sealed class AgentSignOutHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleAsync_WithoutAUserId_TouchesNothing(string userId)
    {
        // Arrange
        var presence = new Mock<IAgentPresenceManager>(MockBehavior.Strict);
        var revoker = new Mock<ISoftPhoneCredentialRevoker>(MockBehavior.Strict);

        var handler = CreateHandler(presence, [revoker.Object]);

        // Act
        await handler.HandleAsync(userId);

        // Assert
        presence.VerifyNoOtherCalls();
        revoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_SignsThePresenceOut_AndRevokesEveryCredential()
    {
        // Arrange
        var presence = new Mock<IAgentPresenceManager>();
        var first = CreateRevoker("first");
        var second = CreateRevoker("second");

        var handler = CreateHandler(presence, [first.Object, second.Object]);

        // Act
        await handler.HandleAsync("user-1");

        // Assert
        presence.Verify(manager => manager.SignOutAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
        first.Verify(revoker => revoker.RevokeForUserAsync("user-1", "signed-out", It.IsAny<CancellationToken>()), Times.Once);
        second.Verify(revoker => revoker.RevokeForUserAsync("user-1", "signed-out", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPresenceThrows_DoesNotPropagate()
    {
        // Arrange
        var presence = new Mock<IAgentPresenceManager>();
        presence
            .Setup(manager => manager.SignOutAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the presence store is unreachable"));

        var handler = CreateHandler(presence, []);

        // Act & Assert: the absence of an exception is the assertion. Propagating here would abort the
        // sign-out and leave the user logged in.
        await handler.HandleAsync("user-1");
    }

    [Fact]
    public async Task HandleAsync_WhenOneRevokerThrows_StillRevokesTheRest()
    {
        // Arrange
        var presence = new Mock<IAgentPresenceManager>();

        var failing = CreateRevoker("failing");
        failing
            .Setup(revoker => revoker.RevokeForUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the provider is unreachable"));

        var healthy = CreateRevoker("healthy");

        var handler = CreateHandler(presence, [failing.Object, healthy.Object]);

        // Act
        await handler.HandleAsync("user-1");

        // Assert: one provider being down must not leave another provider's credentials live.
        healthy.Verify(
            revoker => revoker.RevokeForUserAsync("user-1", "signed-out", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<ISoftPhoneCredentialRevoker> CreateRevoker(string providerName)
    {
        var revoker = new Mock<ISoftPhoneCredentialRevoker>();
        revoker.SetupGet(instance => instance.ProviderName).Returns(providerName);

        return revoker;
    }

    private static DefaultAgentSignOutHandler CreateHandler(
        Mock<IAgentPresenceManager> presence,
        IEnumerable<ISoftPhoneCredentialRevoker> revokers)
        => new(
            presence.Object,
            revokers,
            TimeProvider.System,
            NullLogger<DefaultAgentSignOutHandler>.Instance);
}
