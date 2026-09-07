using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Workflow events are how a site owner reacts to a subscription changing state without writing code.
/// These tests pin which transition raises which event, and that a write which changes nothing about the
/// status raises none, so a customer is not messaged every time a sweep touches their record.
/// </summary>
public sealed class WorkflowSubscriptionLifecycleHandlerTests
{
    [Theory]
    [InlineData(SubscriptionStatus.Incomplete, SubscriptionStatus.Active, SubscriptionStartedEvent.EventName)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Active, SubscriptionRenewedEvent.EventName)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.PastDue, SubscriptionPastDueEvent.EventName)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Canceled, SubscriptionCanceledEvent.EventName)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Expired, SubscriptionExpiredEvent.EventName)]
    public async Task ChangedAsync_RaisesTheEventForTheTransition(SubscriptionStatus previous, SubscriptionStatus current, string expectedEventName)
    {
        // Arrange
        var raised = new List<string>();
        var handler = CreateHandler(raised);

        // Act
        await handler.ChangedAsync(new SubscriptionLifecycleContext(CreateSubscription(current), previous));

        // Assert
        Assert.Equal([expectedEventName], raised);
    }

    /// <summary>
    /// A sweep or a provider sync that only moves dates must not tell a customer their payment failed.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenTheStatusDidNotChange_RaisesNothing()
    {
        // Arrange
        var raised = new List<string>();
        var handler = CreateHandler(raised);

        // Act
        await handler.ChangedAsync(new SubscriptionLifecycleContext(CreateSubscription(SubscriptionStatus.Active), SubscriptionStatus.Active));

        // Assert
        Assert.Empty(raised);
    }

    [Fact]
    public async Task ChangedAsync_ForAStatusWithNoEvent_RaisesNothing()
    {
        // Arrange
        var raised = new List<string>();
        var handler = CreateHandler(raised);

        // Act
        await handler.ChangedAsync(new SubscriptionLifecycleContext(CreateSubscription(SubscriptionStatus.Paused), SubscriptionStatus.Active));

        // Assert
        Assert.Empty(raised);
    }

    /// <summary>
    /// A workflow that throws must not roll back the transition that triggered it. The agreement's state is
    /// the fact.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenTheWorkflowThrows_DoesNotPropagate()
    {
        // Arrange
        var workflowManager = new Mock<IWorkflowManager>();
        workflowManager
            .Setup(manager => manager.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("The workflow store is unavailable."));

        var handler = new WorkflowSubscriptionLifecycleHandler(
            workflowManager.Object,
            NullLogger<WorkflowSubscriptionLifecycleHandler>.Instance);

        // Act & Assert (no exception)
        await handler.ChangedAsync(new SubscriptionLifecycleContext(CreateSubscription(SubscriptionStatus.Canceled), SubscriptionStatus.Active));
    }

    private static Subscription CreateSubscription(SubscriptionStatus status)
        => new()
        {
            ItemId = "sub-1",
            Title = "Membership",
            OwnerId = "owner-1",
            Status = status,
            Currency = "USD",
            Amount = 20m,
            BillingDuration = 1,
            DurationType = DurationType.Month,
        };

    private static WorkflowSubscriptionLifecycleHandler CreateHandler(List<string> raised)
    {
        var workflowManager = new Mock<IWorkflowManager>();
        workflowManager
            .Setup(manager => manager.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback((string name, IDictionary<string, object> _, string _, bool _, bool _) => raised.Add(name))
            .ReturnsAsync([]);

        return new WorkflowSubscriptionLifecycleHandler(
            workflowManager.Object,
            NullLogger<WorkflowSubscriptionLifecycleHandler>.Instance);
    }
}
