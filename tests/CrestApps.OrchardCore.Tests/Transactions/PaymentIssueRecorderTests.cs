using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A money operation that failed is saved as an open payment issue, and a failure to save it never interrupts the code
/// that is already handling the first failure.
/// </summary>
public sealed class PaymentIssueRecorderTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordAsync_SavesTheIssueAsOpen_WithItsIdAndTime()
    {
        var saved = new List<PaymentIssue>();
        var store = new Mock<IPaymentIssueStore>();
        store.Setup(value => value.CreateAsync(It.IsAny<PaymentIssue>(), It.IsAny<CancellationToken>()))
            .Callback((PaymentIssue issue, CancellationToken _) => saved.Add(issue))
            .Returns(ValueTask.CompletedTask);

        var recorder = CreateRecorder(store.Object);

        Assert.True(await recorder.RecordAsync(new PaymentIssue { Kind = PaymentIssueKinds.RefundFailed, Status = PaymentIssueStatus.Resolved }, TestContext.Current.CancellationToken));

        var issue = Assert.Single(saved);

        Assert.Equal(PaymentIssueStatus.Open, issue.Status);
        Assert.Equal(26, issue.ItemId.Length);
        Assert.Equal(_now, issue.CreatedUtc);
    }

    [Fact]
    public async Task RecordAsync_WhenTheStoreFails_ReturnsFalseInsteadOfThrowing()
    {
        var store = new Mock<IPaymentIssueStore>();
        store.Setup(value => value.CreateAsync(It.IsAny<PaymentIssue>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The database is down."));

        var recorder = CreateRecorder(store.Object);

        Assert.False(await recorder.RecordAsync(new PaymentIssue { Kind = PaymentIssueKinds.RefundFailed }, TestContext.Current.CancellationToken));
    }

    private static IPaymentIssueRecorder CreateRecorder(IPaymentIssueStore store)
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        var services = new ServiceCollection()
            .AddSingleton(store)
            .AddSingleton(clock.Object)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddPaymentIssues();

        return services.BuildServiceProvider().GetRequiredService<IPaymentIssueRecorder>();
    }
}
