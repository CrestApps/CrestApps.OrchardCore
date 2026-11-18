using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Stores estimates in the <c>Invoicing</c> collection.
/// </summary>
public interface IEstimateStore : ICatalog<Estimate>
{
    /// <summary>
    /// Finds an estimate by its number.
    /// </summary>
    Task<Estimate> FindByNumberAsync(string number, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the sent estimates whose expiry date is before a day, at most <paramref name="max"/>.
    /// </summary>
    Task<IReadOnlyList<Estimate>> ListExpiringAsync(DateOnly before, int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of estimates, newest first.
    /// </summary>
    Task<PageResult<Estimate>> PageAsync(int page, int pageSize, EstimateQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IEstimateStore"/>, with concurrency checks.
/// </summary>
public sealed class EstimateStore : DocumentCatalog<Estimate, EstimateIndex>, IEstimateStore
{
    public EstimateStore(ISession session, IClock clock)
        : base(session, clock)
    {
        CollectionName = InvoicingConstants.CollectionName;
    }

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <inheritdoc/>
    public Task<Estimate> FindByNumberAsync(string number, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(number)
            ? Task.FromResult<Estimate>(null)
            : Session.Query<Estimate, EstimateIndex>(index => index.Number == number.Trim(), collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Estimate>> ListExpiringAsync(DateOnly before, int max, CancellationToken cancellationToken = default)
    {
        var sent = (int)EstimateStatus.Sent;
        var day = before.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var estimates = await Session.Query<Estimate, EstimateIndex>(index => index.Status == sent && index.ExpiryDate != null && index.ExpiryDate < day, collection: CollectionName)
            .OrderBy(index => index.ExpiryDate)
            .Take(max)
            .ListAsync(cancellationToken);

        return [.. estimates];
    }

    /// <inheritdoc/>
    public async Task<PageResult<Estimate>> PageAsync(int page, int pageSize, EstimateQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var records = Session.Query<Estimate, EstimateIndex>(collection: CollectionName);

        if (query.Status.HasValue)
        {
            var status = (int)query.Status.Value;
            records = records.Where(index => index.Status == status);
        }

        var billTo = (query.BillTo ?? []).Where(party => !party.IsEmpty).Distinct().ToArray();

        if (billTo.Length > 0)
        {
            Expression<Func<EstimateIndex, bool>> predicate = null;

            foreach (var group in billTo.GroupBy(party => party.Kind, StringComparer.Ordinal))
            {
                var kind = group.Key;
                var ids = group.Select(party => party.Id).Distinct(StringComparer.Ordinal).ToArray();
                Expression<Func<EstimateIndex, bool>> ofKind = index => index.BillToKind == kind && index.BillToId.IsIn(ids);

                predicate = IndexPredicates.OrElse(predicate, ofKind);
            }

            records = records.Where(predicate);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            records = records.Where(index => index.Number.Contains(search) || index.BillToName.Contains(search) || index.Reference.Contains(search));
        }

        var ordered = records
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.ItemId);

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<Estimate>
        {
            Count = await ordered.CountAsync(cancellationToken),
            Entries = (await ordered.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }
}

/// <summary>
/// The default <see cref="IEstimateManager"/>.
/// </summary>
public sealed class EstimateManager : CatalogManager<Estimate>, IEstimateManager
{
    private readonly IEstimateStore _store;

    public EstimateManager(IEstimateStore store, IEnumerable<ICatalogEntryHandler<Estimate>> handlers, ILogger<CatalogManager<Estimate>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public Task<PageResult<Estimate>> PageAsync(int page, int pageSize, EstimateQuery query, CancellationToken cancellationToken = default)
        => _store.PageAsync(page, pageSize, query, cancellationToken);
}

/// <summary>
/// Audits estimates saved through <see cref="IEstimateManager"/>, refuses lines out of range on a draft, and refuses any
/// change to a sent estimate: its offer changes only through a revision.
/// </summary>
public sealed class EstimateHandler : AuditedCatalogEntryHandlerBase<Estimate>
{
    internal readonly IStringLocalizer S;

    public EstimateHandler(IHttpContextAccessor httpContextAccessor, IClock clock, IStringLocalizer<EstimateHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<Estimate> context, CancellationToken cancellationToken = default)
    {
        var estimate = context.Model;

        if (estimate.Status != EstimateStatus.Draft || !string.IsNullOrEmpty(estimate.Number))
        {
            context.Result.Fail(new ValidationResult(S["A sent estimate can't be changed. Revise it instead."]));

            return Task.CompletedTask;
        }

        if (estimate.DepositPercent is < 0m or > 100m)
        {
            context.Result.Fail(new ValidationResult(S["The deposit must be a percent between 0 and 100."], [nameof(Estimate.DepositPercent)]));
        }

        if (estimate.DepositAmount is < 0m)
        {
            context.Result.Fail(new ValidationResult(S["The deposit can't be below zero."], [nameof(Estimate.DepositAmount)]));
        }

        foreach (var error in InvoiceIssueRules.ValidateLines(estimate))
        {
            context.Result.Fail(new ValidationResult(InvoiceIssueMessages.Describe(error, estimate, S), [nameof(Estimate.Lines)]));
        }

        return Task.CompletedTask;
    }
}
