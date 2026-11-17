using System.Globalization;
using CrestApps.OrchardCore.Banking.Checks.Core.Indexes;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.YesSql.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.SourceAccess;
using YesSql;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

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

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<CheckRecord> Checks)> ListPageAsync(CheckRegisterQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var checks = Session.Query<CheckRecord, CheckRecordIndex>(collection: ChecksConstants.CollectionName);

        if (SourceAccessExpressions.Restrict<CheckRecordIndex>(query.Access ?? SourceAccessFilter.Unrestricted) is { } restrict)
        {
            checks = checks.Where(restrict);
        }

        if (!string.IsNullOrEmpty(query.BankAccountId))
        {
            var bankAccountId = query.BankAccountId;
            checks = checks.Where(index => index.BankAccountId == bankAccountId);
        }

        if (query.Status is { } status)
        {
            var statusName = status.ToString();
            checks = checks.Where(index => index.Status == statusName);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            if (long.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                checks = checks.Where(index => index.Number == number);
            }
            else
            {
                checks = checks.Where(index => index.PayeeName.Contains(search));
            }
        }

        var count = await checks.CountAsync(cancellationToken);
        var ordered = query.OldestFirst
            ? checks.OrderBy(index => index.IssuedUtc).ThenBy(index => index.Number)
            : checks.OrderByDescending(index => index.IssuedUtc).ThenByDescending(index => index.Number);
        var page = await ordered.Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToList());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CheckRecord>> ListPrintedBeforeAsync(DateOnly checkDateBefore, int take, CancellationToken cancellationToken = default)
    {
        var printed = nameof(CheckStatus.Printed);
        var before = checkDateBefore.ToDateTime(TimeOnly.MinValue);
        var checks = await Session.Query<CheckRecord, CheckRecordIndex>(index => index.Status == printed && index.CheckDate < before, collection: ChecksConstants.CollectionName)
            .OrderBy(index => index.CheckDate)
            .ThenBy(index => index.Number)
            .Take(take)
            .ListAsync(cancellationToken);

        return checks.ToList();
    }
}
