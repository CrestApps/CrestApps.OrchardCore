using CrestApps.OrchardCore.Checks.Core.Indexes;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Checks.Core.Services;

/// <summary>
/// Stores the check register in YesSql.
/// </summary>
public sealed class CheckRecordStore : DocumentCatalog<CheckRecord, CheckRecordIndex>, ICheckRecordStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckRecordStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public CheckRecordStore(ISession session)
        : base(session)
    {
        CollectionName = ChecksConstants.CollectionName;
    }

    /// <inheritdoc />
    public Task<CheckRecord> FindByNumberAsync(string bankAccountId, long number, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bankAccountId);

        return Session.Query<CheckRecord, CheckRecordIndex>(index => index.BankAccountId == bankAccountId && index.Number == number, collection: ChecksConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long?> GetHighestNumberAsync(string bankAccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bankAccountId);

        var highest = await Session.QueryIndex<CheckRecordIndex>(index => index.BankAccountId == bankAccountId, collection: ChecksConstants.CollectionName)
            .OrderByDescending(index => index.Number)
            .FirstOrDefaultAsync(cancellationToken);

        return highest?.Number;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CheckRecord>> ListBySourceAsync(string sourceKind, string sourceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceKind);
        ArgumentException.ThrowIfNullOrEmpty(sourceId);

        var checks = await Session.Query<CheckRecord, CheckRecordIndex>(index => index.SourceKind == sourceKind && index.SourceId == sourceId, collection: ChecksConstants.CollectionName)
            .OrderBy(index => index.IssuedUtc)
            .ListAsync(cancellationToken);

        return checks.ToList();
    }
}
