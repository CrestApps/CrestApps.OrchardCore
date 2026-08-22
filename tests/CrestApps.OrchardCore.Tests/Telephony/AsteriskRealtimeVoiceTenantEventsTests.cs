using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskRealtimeVoiceTenantEventsTests
{
    [Fact]
    public async Task ActivatingAsync_WhenProviderConfigurationIsIncomplete_DoesNotStartTheListenerOrClaimOwnership()
    {
        // Arrange
        var listener = new RecordingListener();
        var gate = new Mock<IAsteriskAriApplicationGate>(MockBehavior.Strict);

        var events = CreateEvents(
            listener,
            gate.Object,
            new DefaultAsteriskOptions
            {
                IsEnabled = true,

                // BaseUrl/UserName/Password/ApplicationName intentionally omitted: provider-local validation must fail.
            });

        // Act
        await events.ActivatingAsync();

        // Assert
        Assert.False(listener.WasStarted);
        gate.Verify(g => g.TryAcquire(It.IsAny<AsteriskResolvedSettings>()), Times.Never);
    }

    [Fact]
    public async Task ActivatingAsync_WhenApplicationOwnershipIsDenied_DoesNotStartTheListener()
    {
        // Arrange
        var listener = new RecordingListener();
        var gate = new Mock<IAsteriskAriApplicationGate>();
        gate.Setup(g => g.TryAcquire(It.IsAny<AsteriskResolvedSettings>())).Returns(false);

        var events = CreateEvents(listener, gate.Object, CreateValidOptions());

        // Act
        await events.ActivatingAsync();

        // Assert
        Assert.False(listener.WasStarted);
        gate.Verify(g => g.TryAcquire(It.IsAny<AsteriskResolvedSettings>()), Times.Once);
    }

    [Fact]
    public async Task ActivatingAsync_WhenValidationPassesAndOwnershipIsAcquired_StartsTheListener()
    {
        // Arrange
        var listener = new RecordingListener();
        var gate = new Mock<IAsteriskAriApplicationGate>();
        gate.Setup(g => g.TryAcquire(It.IsAny<AsteriskResolvedSettings>())).Returns(true);

        var events = CreateEvents(listener, gate.Object, CreateValidOptions());

        // Act
        await events.ActivatingAsync();

        // Assert
        Assert.True(listener.WasStarted);
        Assert.NotNull(listener.StartedWith);
        var started = Assert.Single(listener.StartedWith);
        Assert.Equal("contact-center-Default", started.ApplicationName);
    }

    private static DefaultAsteriskOptions CreateValidOptions()
        => new()
        {
            IsEnabled = true,
            BaseUrl = "https://pbx.example.test",
            UserName = "ari-user",
            Password = "ari-secret",
            ApplicationName = "contact-center",
        };

    private static AsteriskRealtimeVoiceTenantEvents CreateEvents(
        IAsteriskRealtimeVoiceListener listener,
        IAsteriskAriApplicationGate gate,
        DefaultAsteriskOptions defaultOptions)
    {
        return new AsteriskRealtimeVoiceTenantEvents(
            SiteServiceFactory.Create(new AsteriskSettings { IsEnabled = false }),
            Mock.Of<IDataProtectionProvider>(),
            Options.Create(defaultOptions),
            new ShellSettings { Name = ShellSettings.DefaultShellName },
            listener,
            gate,
            NullLogger<AsteriskRealtimeVoiceTenantEvents>.Instance);
    }

    private sealed class RecordingListener : IAsteriskRealtimeVoiceListener
    {
        public bool WasStarted { get; private set; }

        public IReadOnlyList<AsteriskResolvedSettings> StartedWith { get; private set; }

        public Task StartAsync(IReadOnlyList<AsteriskResolvedSettings> listeners)
        {
            WasStarted = true;
            StartedWith = listeners;

            return Task.CompletedTask;
        }

        public Task StopAsync()
            => Task.CompletedTask;
    }
}
