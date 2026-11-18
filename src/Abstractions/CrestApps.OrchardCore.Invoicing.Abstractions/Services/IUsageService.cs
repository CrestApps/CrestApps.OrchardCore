using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Manages the usage meters.
/// </summary>
public interface IUsageMeterManager : ICatalogManager<UsageMeter>
{
    /// <summary>
    /// Finds a meter by its code.
    /// </summary>
    Task<UsageMeter> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// Records customers' usage and adds it up for billing.
/// </summary>
public interface IUsageService
{
    /// <summary>
    /// Records usage of a meter for a customer, once per event id: recording the same event again returns the record
    /// made the first time.
    /// </summary>
    Task<UsageRecordResult> RecordAsync(UsageRecordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds up a customer's usage of a meter from one moment (inclusive) to another (exclusive), as the meter says.
    /// </summary>
    Task<decimal> GetTotalAsync(string meterId, PartyRef party, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a meter's latest records, newest first.
    /// </summary>
    Task<IReadOnlyList<UsageRecord>> ListRecentAsync(string meterId, int count, CancellationToken cancellationToken = default);
}

/// <summary>
/// Usage to record.
/// </summary>
public sealed class UsageRecordRequest
{
    /// <summary>
    /// Gets or sets the meter's code (or id).
    /// </summary>
    public string Meter { get; set; }

    public PartyRef Party { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets when it happened, in UTC; now when not given.
    /// </summary>
    public DateTime? OccurredUtc { get; set; }

    public string EventId { get; set; }
}

/// <summary>
/// What recording usage came to.
/// </summary>
public sealed class UsageRecordResult
{
    public bool Succeeded
        => Errors.Count == 0;

    public UsageRecord Record { get; set; }

    /// <summary>
    /// Gets or sets whether the event was recorded before, so nothing new was recorded.
    /// </summary>
    public bool AlreadyRecorded { get; set; }

    public IList<string> Errors { get; } = [];

    public UsageRecordResult Fail(string error)
    {
        Errors.Add(error);

        return this;
    }
}
