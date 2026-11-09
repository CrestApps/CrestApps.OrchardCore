using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks;

/// <summary>
/// The provider webhook inbox task moved from Contact Center to the Webhooks module. Orchard keys a task's saved
/// schedule by its full type name, so the task keeps the name it had, and Contact Center still holds the task back while
/// its Voice feature drains, as it did before the move.
/// </summary>
public sealed class ProviderWebhookInboxTaskUpgradeTests
{
    [Fact]
    public void TheTask_KeepsTheTypeNameItsSavedScheduleIsKeyedBy()
    {
        Assert.Equal(
            "CrestApps.OrchardCore.ContactCenter.BackgroundTasks.ProviderWebhookInboxBackgroundTask",
            typeof(ProviderWebhookInboxBackgroundTask).FullName);
    }

    [Fact]
    public async Task AGateThatRefuses_SkipsThePass()
    {
        // Arrange
        var inbox = new Mock<IProviderWebhookInbox>();
        var admitted = new Mock<IProviderWebhookInboxDispatchGate>();
        var lease = new Mock<IDisposable>();
        admitted.Setup(gate => gate.TryEnter()).Returns(lease.Object);
        var refusing = new Mock<IProviderWebhookInboxDispatchGate>();
        refusing.Setup(gate => gate.TryEnter()).Returns((IDisposable)null);

        var services = CreateServices(inbox.Object, admitted.Object, refusing.Object);

        // Act
        await new ProviderWebhookInboxBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        inbox.Verify(value => value.DispatchDueAsync(It.IsAny<CancellationToken>()), Times.Never);
        lease.Verify(value => value.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GatesThatAdmit_RunThePass_AndReleaseTheirLeases()
    {
        // Arrange
        var inbox = new Mock<IProviderWebhookInbox>();
        var gate = new Mock<IProviderWebhookInboxDispatchGate>();
        var lease = new Mock<IDisposable>();
        gate.Setup(value => value.TryEnter()).Returns(lease.Object);

        var services = CreateServices(inbox.Object, gate.Object);

        // Act
        await new ProviderWebhookInboxBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        inbox.Verify(value => value.DispatchDueAsync(It.IsAny<CancellationToken>()), Times.Once);
        lease.Verify(value => value.Dispose(), Times.Once);
    }

    [Fact]
    public void TheContactCenterGate_RefusesWhileVoiceIsQuiescing()
    {
        // Arrange
        var lease = Mock.Of<IContactCenterFeatureWorkLease>();
        var open = new Mock<IContactCenterFeatureWorkManager>();
        open.Setup(manager => manager.TryEnter(ContactCenterConstants.Feature.Voice)).Returns(lease);
        var quiescing = new Mock<IContactCenterFeatureWorkManager>();
        quiescing.Setup(manager => manager.TryEnter(ContactCenterConstants.Feature.Voice)).Returns((IContactCenterFeatureWorkLease)null);

        // Act and assert
        Assert.Same(lease, new VoiceProviderWebhookInboxDispatchGate(open.Object).TryEnter());
        Assert.Null(new VoiceProviderWebhookInboxDispatchGate(quiescing.Object).TryEnter());
    }

    private static ServiceProvider CreateServices(IProviderWebhookInbox inbox, params IProviderWebhookInboxDispatchGate[] gates)
    {
        var services = new ServiceCollection()
            .AddSingleton(inbox)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        foreach (var gate in gates)
        {
            services.AddSingleton(gate);
        }

        return services.BuildServiceProvider();
    }
}
