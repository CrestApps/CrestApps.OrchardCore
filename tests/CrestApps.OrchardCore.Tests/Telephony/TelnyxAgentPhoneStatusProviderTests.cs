using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelnyxAgentPhoneStatusProviderTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 17, 32, 40, DateTimeKind.Utc);

    [Fact]
    public async Task HasOpenPhoneAsync_CredentialRegisteredFromAnOpenWindow_IsOpen()
    {
        // Arrange
        var provider = Provider(Credential(registered: Now.AddMinutes(-5)));

        // Act
        var open = await provider.HasOpenPhoneAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(open);
    }

    [Fact]
    public async Task HasOpenPhoneAsync_EveryWindowClosedOrRefusedSince_IsNotOpen()
    {
        // Arrange
        // One window closed; the other never closed its connection (the server restarted under it), but a leg rung to
        // it was refused as unavailable after it registered.
        var provider = Provider(
            Credential(registered: Now.AddMinutes(-9), closed: Now.AddMinutes(-8)),
            Credential(registered: Now.AddMinutes(-5), unreachable: Now.AddMinutes(-1)),
            Credential(registered: null));

        // Act
        var open = await provider.HasOpenPhoneAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(open);
    }

    [Fact]
    public async Task HasOpenPhoneAsync_RegisteredAgainAfterARefusal_IsOpen()
    {
        // Arrange
        var provider = Provider(Credential(registered: Now.AddMinutes(-1), unreachable: Now.AddMinutes(-2)));

        // Act
        var open = await provider.HasOpenPhoneAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(open);
    }

    [Fact]
    public async Task HasOpenPhoneAsync_TelnyxNotConfigured_CannotTell()
    {
        // Arrange
        var store = new Mock<ITelnyxAgentCredentialStore>(MockBehavior.Strict);
        var provider = new TelnyxAgentPhoneStatusProvider(
            store.Object,
            new FixedOptionsMonitor(new TelnyxOptions()),
            new StubClock(Now));

        // Act
        var open = await provider.HasOpenPhoneAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(open);
    }

    private static TelnyxAgentPhoneStatusProvider Provider(params TelnyxAgentCredential[] credentials)
    {
        var store = new Mock<ITelnyxAgentCredentialStore>();
        store
            .Setup(value => value.ListLiveByUserAsync("user-1", Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);

        return new TelnyxAgentPhoneStatusProvider(
            store.Object,
            new FixedOptionsMonitor(new TelnyxOptions { IsEnabled = true, ApiKey = "key", ConnectionId = "connection" }),
            new StubClock(Now));
    }

    private static TelnyxAgentCredential Credential(DateTime? registered, DateTime? closed = null, DateTime? unreachable = null)
        => new()
        {
            UserId = "user-1",
            CredentialId = Guid.NewGuid().ToString("N"),
            RegisteredUtc = registered,
            ConnectionClosedUtc = closed,
            UnreachableUtc = unreachable,
        };

    private sealed class FixedOptionsMonitor : IOptionsMonitor<TelnyxOptions>
    {
        public FixedOptionsMonitor(TelnyxOptions value)
        {
            CurrentValue = value;
        }

        public TelnyxOptions CurrentValue { get; }

        public TelnyxOptions Get(string name)
            => CurrentValue;

        public IDisposable OnChange(Action<TelnyxOptions, string> listener)
            => null;
    }
}
