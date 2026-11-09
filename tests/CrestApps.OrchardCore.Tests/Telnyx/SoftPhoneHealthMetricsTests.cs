using CrestApps.OrchardCore.Telnyx.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telnyx;

public sealed class SoftPhoneHealthMetricsTests
{
    private static readonly DateTime _startedUtc = new(2026, 11, 4, 9, 30, 0, DateTimeKind.Utc);
    private static readonly IClock _clock = Mock.Of<IClock>(clock => clock.UtcNow == _startedUtc);

    [Fact]
    public void GetSnapshot_ReportsCountersSinceTheClockTimeTheyStarted()
    {
        var metrics = new SoftPhoneHealthMetrics(_clock);

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(new DateTimeOffset(_startedUtc), snapshot.SinceUtc);
    }

    [Fact]
    public void GetSnapshot_WithNoActivity_ReportsPerfectRates()
    {
        var metrics = new SoftPhoneHealthMetrics(_clock);

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(0, snapshot.CredentialsIssued);
        Assert.Equal(0, snapshot.CredentialFailures);
        Assert.Equal(1d, snapshot.CredentialSuccessRate);
        Assert.Equal(1d, snapshot.WebhookSuccessRate);
    }

    [Fact]
    public void RecordCredential_CountsAndComputesSuccessRate()
    {
        var metrics = new SoftPhoneHealthMetrics(_clock);

        metrics.RecordCredentialIssued();
        metrics.RecordCredentialIssued();
        metrics.RecordCredentialIssued();
        metrics.RecordCredentialFailure();

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(3, snapshot.CredentialsIssued);
        Assert.Equal(1, snapshot.CredentialFailures);
        Assert.Equal(0.75d, snapshot.CredentialSuccessRate, 5);
    }

    [Fact]
    public void RecordWebhookProcessed_CountsSuccessAndFailureSeparately()
    {
        var metrics = new SoftPhoneHealthMetrics(_clock);

        metrics.RecordWebhookProcessed(true);
        metrics.RecordWebhookProcessed(true);
        metrics.RecordWebhookProcessed(false);

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(2, snapshot.WebhooksProcessed);
        Assert.Equal(1, snapshot.WebhookFailures);
        Assert.Equal(2d / 3d, snapshot.WebhookSuccessRate, 5);
    }

    [Fact]
    public void Counters_AreThreadSafe()
    {
        var metrics = new SoftPhoneHealthMetrics(_clock);

        Parallel.For(0, 1000, _ =>
        {
            metrics.RecordCredentialIssued();
            metrics.RecordWebhookProcessed(true);
        });

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(1000, snapshot.CredentialsIssued);
        Assert.Equal(1000, snapshot.WebhooksProcessed);
    }
}
