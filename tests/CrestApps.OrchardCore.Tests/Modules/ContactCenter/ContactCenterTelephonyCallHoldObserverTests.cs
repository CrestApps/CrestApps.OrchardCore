using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterTelephonyCallHoldObserverTests
{
    [Fact]
    public async Task CallHoldChanged_RecordsTheChange()
    {
        // Arrange
        var recorder = new Mock<IAgentCallHoldRecorder>();
        var observer = new ContactCenterTelephonyCallHoldObserver(
            recorder.Object,
            new Mock<IContactCenterScopeExecutor>().Object,
            NullLogger<ContactCenterTelephonyCallHoldObserver>.Instance);
        var change = new TelephonyCallHoldChange { CallId = "call-1", UserId = "user-1", IsOnHold = true };

        // Act
        await observer.CallHoldChangedAsync(change, TestContext.Current.CancellationToken);

        // Assert
        recorder.Verify(value => value.RecordAsync(change, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CallHoldChanged_ThatLostARace_IsRetriedOnceInAFreshScope()
    {
        // Arrange
        var recorder = new Mock<IAgentCallHoldRecorder>();
        recorder
            .Setup(value => value.RecordAsync(It.IsAny<TelephonyCallHoldChange>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()));

        var retryRecorder = new Mock<IAgentCallHoldRecorder>();
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(value => value.ExecuteAsync(It.IsAny<Func<IAgentCallHoldRecorder, Task>>()))
            .Returns<Func<IAgentCallHoldRecorder, Task>>(operation => operation(retryRecorder.Object));

        var observer = new ContactCenterTelephonyCallHoldObserver(
            recorder.Object,
            scopeExecutor.Object,
            NullLogger<ContactCenterTelephonyCallHoldObserver>.Instance);
        var change = new TelephonyCallHoldChange { CallId = "call-1", UserId = "user-1", IsOnHold = true };

        // Act
        await observer.CallHoldChangedAsync(change, TestContext.Current.CancellationToken);

        // Assert
        retryRecorder.Verify(value => value.RecordAsync(change, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CallHoldChanged_ThatFails_DoesNotThrow()
    {
        // Arrange
        var recorder = new Mock<IAgentCallHoldRecorder>();
        recorder
            .Setup(value => value.RecordAsync(It.IsAny<TelephonyCallHoldChange>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store unavailable"));

        var observer = new ContactCenterTelephonyCallHoldObserver(
            recorder.Object,
            new Mock<IContactCenterScopeExecutor>().Object,
            NullLogger<ContactCenterTelephonyCallHoldObserver>.Instance);

        // Act
        var exception = await Record.ExceptionAsync(() => observer.CallHoldChangedAsync(
            new TelephonyCallHoldChange { CallId = "call-1", UserId = "user-1", IsOnHold = true },
            TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }
}
