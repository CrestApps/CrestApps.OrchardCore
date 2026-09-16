using System.Threading;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A point-in-time snapshot of the soft-phone health counters.
/// </summary>
public sealed class SoftPhoneHealthSnapshot
{
    /// <summary>
    /// Gets the time the counters started accumulating (process/shell start).
    /// </summary>
    public DateTimeOffset SinceUtc { get; init; }

    /// <summary>
    /// Gets the number of browser SIP credentials successfully issued (each is a soft-phone registration).
    /// </summary>
    public long CredentialsIssued { get; init; }

    /// <summary>
    /// Gets the number of credential issuance attempts that failed.
    /// </summary>
    public long CredentialFailures { get; init; }

    /// <summary>
    /// Gets the number of inbound Telnyx webhooks processed successfully.
    /// </summary>
    public long WebhooksProcessed { get; init; }

    /// <summary>
    /// Gets the number of inbound Telnyx webhooks that failed processing (rejected or errored).
    /// </summary>
    public long WebhookFailures { get; init; }

    /// <summary>
    /// Gets the credential issuance success rate (0-1), or 1 when nothing has been attempted.
    /// </summary>
    public double CredentialSuccessRate => Rate(CredentialsIssued, CredentialFailures);

    /// <summary>
    /// Gets the webhook processing success rate (0-1), or 1 when nothing has been received.
    /// </summary>
    public double WebhookSuccessRate => Rate(WebhooksProcessed, WebhookFailures);

    private static double Rate(long ok, long failed)
    {
        var total = ok + failed;

        return total == 0 ? 1d : (double)ok / total;
    }
}

/// <summary>
/// Records soft-phone health signals -- browser credential issuance (a proxy for registration success) and
/// inbound webhook processing -- so registration-success rate, credential-issuance failures, and webhook
/// delivery can be surfaced for dashboards, alerts, and the health canary.
/// </summary>
public interface ISoftPhoneHealthMetrics
{
    /// <summary>
    /// Records a successful browser credential issuance (a soft-phone registration).
    /// </summary>
    void RecordCredentialIssued();

    /// <summary>
    /// Records a failed browser credential issuance attempt.
    /// </summary>
    void RecordCredentialFailure();

    /// <summary>
    /// Records the outcome of processing an inbound Telnyx webhook.
    /// </summary>
    /// <param name="succeeded">Whether the webhook was accepted and processed.</param>
    void RecordWebhookProcessed(bool succeeded);

    /// <summary>
    /// Gets a snapshot of the current counters.
    /// </summary>
    SoftPhoneHealthSnapshot GetSnapshot();
}

/// <summary>
/// Default in-memory <see cref="ISoftPhoneHealthMetrics"/>. Counters are cumulative for the life of the shell
/// and updated with lock-free interlocked increments so recording is safe from any thread without contending
/// with call handling.
/// </summary>
public sealed class SoftPhoneHealthMetrics : ISoftPhoneHealthMetrics
{
    private readonly DateTimeOffset _since = DateTimeOffset.UtcNow;
    private long _credentialsIssued;
    private long _credentialFailures;
    private long _webhooksProcessed;
    private long _webhookFailures;

    /// <inheritdoc/>
    public void RecordCredentialIssued()
        => Interlocked.Increment(ref _credentialsIssued);

    /// <inheritdoc/>
    public void RecordCredentialFailure()
        => Interlocked.Increment(ref _credentialFailures);

    /// <inheritdoc/>
    public void RecordWebhookProcessed(bool succeeded)
    {
        if (succeeded)
        {
            Interlocked.Increment(ref _webhooksProcessed);
        }
        else
        {
            Interlocked.Increment(ref _webhookFailures);
        }
    }

    /// <inheritdoc/>
    public SoftPhoneHealthSnapshot GetSnapshot()
        => new()
        {
            SinceUtc = _since,
            CredentialsIssued = Interlocked.Read(ref _credentialsIssued),
            CredentialFailures = Interlocked.Read(ref _credentialFailures),
            WebhooksProcessed = Interlocked.Read(ref _webhooksProcessed),
            WebhookFailures = Interlocked.Read(ref _webhookFailures),
        };
}
