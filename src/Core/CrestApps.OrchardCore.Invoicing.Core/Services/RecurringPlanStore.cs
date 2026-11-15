using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars.Recurrence;
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
using YesSql.Indexes;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Indexes
{
    /// <summary>
    /// The index of recurring plans: what their list and the run sweep search.
    /// </summary>
    public sealed class RecurringPlanIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
    {
        public string Name { get; set; }

        public int Status { get; set; }

        public string BillToName { get; set; }

        public string Reference { get; set; }

        public string Currency { get; set; }

        public decimal Total { get; set; }

        public DateTime? NextRunDate { get; set; }

        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>
    /// Maps recurring plans to <see cref="RecurringPlanIndex"/>.
    /// </summary>
    public sealed class RecurringPlanIndexProvider : IndexProvider<RecurringPlan>
    {
        public RecurringPlanIndexProvider()
        {
            CollectionName = InvoicingConstants.CollectionName;
        }

        /// <inheritdoc/>
        public override void Describe(DescribeContext<RecurringPlan> context)
        {
            context
                .For<RecurringPlanIndex>()
                .Map(plan => new RecurringPlanIndex
                {
                    ItemId = plan.ItemId,
                    Name = Truncate(plan.Name),
                    Status = (int)plan.Status,
                    BillToName = Truncate(plan.BillTo?.Contact?.Name),
                    Reference = Truncate(plan.Reference),
                    Currency = plan.Currency,
                    Total = plan.Totals?.Total ?? 0m,
                    NextRunDate = plan.Status == RecurringPlanStatus.Active ? plan.NextRunDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null,
                    CreatedUtc = plan.CreatedUtc,
                });
        }

        private static string Truncate(string value)
            => string.IsNullOrEmpty(value) ? null : value.Length <= InvoiceIndexProvider.TextLength ? value : value[..InvoiceIndexProvider.TextLength];
    }
}

namespace CrestApps.OrchardCore.Invoicing.Core.Services
{
    /// <summary>
    /// Stores recurring plans in the <c>Invoicing</c> collection.
    /// </summary>
    public interface IRecurringPlanStore : ICatalog<RecurringPlan>
    {
        /// <summary>
        /// Lists the active plans with a run due on or before a day, at most <paramref name="max"/>.
        /// </summary>
        Task<IReadOnlyList<RecurringPlan>> ListDueAsync(DateOnly today, int max, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a page of plans, newest first.
        /// </summary>
        Task<PageResult<RecurringPlan>> PageAsync(int page, int pageSize, RecurringPlanQuery query, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The default <see cref="IRecurringPlanStore"/>, with concurrency checks.
    /// </summary>
    public sealed class RecurringPlanStore : DocumentCatalog<RecurringPlan, RecurringPlanIndex>, IRecurringPlanStore
    {
        public RecurringPlanStore(ISession session, IClock clock)
            : base(session, clock)
        {
            CollectionName = InvoicingConstants.CollectionName;
        }

        /// <inheritdoc/>
        protected override bool CheckConcurrency => true;

        /// <inheritdoc/>
        public async Task<IReadOnlyList<RecurringPlan>> ListDueAsync(DateOnly today, int max, CancellationToken cancellationToken = default)
        {
            var active = (int)RecurringPlanStatus.Active;
            var day = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            var plans = await Session.Query<RecurringPlan, RecurringPlanIndex>(index => index.Status == active && index.NextRunDate != null && index.NextRunDate <= day, collection: CollectionName)
                .OrderBy(index => index.NextRunDate)
                .Take(max)
                .ListAsync(cancellationToken);

            return [.. plans];
        }

        /// <inheritdoc/>
        public async Task<PageResult<RecurringPlan>> PageAsync(int page, int pageSize, RecurringPlanQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var records = Session.Query<RecurringPlan, RecurringPlanIndex>(collection: CollectionName);

            if (query.Status.HasValue)
            {
                var status = (int)query.Status.Value;
                records = records.Where(index => index.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                records = records.Where(index => index.Name.Contains(search) || index.BillToName.Contains(search) || index.Reference.Contains(search));
            }

            var ordered = records.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.ItemId);
            var skip = (Math.Max(page, 1) - 1) * pageSize;

            return new PageResult<RecurringPlan>
            {
                Count = await ordered.CountAsync(cancellationToken),
                Entries = (await ordered.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
            };
        }
    }

    /// <summary>
    /// The default <see cref="IRecurringPlanManager"/>.
    /// </summary>
    public sealed class RecurringPlanManager : CatalogManager<RecurringPlan>, IRecurringPlanManager
    {
        private readonly IRecurringPlanStore _store;

        public RecurringPlanManager(IRecurringPlanStore store, IEnumerable<ICatalogEntryHandler<RecurringPlan>> handlers, ILogger<CatalogManager<RecurringPlan>> logger)
            : base(store, handlers, logger)
        {
            _store = store;
        }

        /// <inheritdoc/>
        public Task<PageResult<RecurringPlan>> PageAsync(int page, int pageSize, RecurringPlanQuery query, CancellationToken cancellationToken = default)
            => _store.PageAsync(page, pageSize, query, cancellationToken);
    }

    /// <summary>
    /// Audits recurring plans saved through <see cref="IRecurringPlanManager"/>, and checks their schedule and lines.
    /// </summary>
    public sealed class RecurringPlanHandler : AuditedCatalogEntryHandlerBase<RecurringPlan>
    {
        internal readonly IStringLocalizer S;

        public RecurringPlanHandler(IHttpContextAccessor httpContextAccessor, IClock clock, IStringLocalizer<RecurringPlanHandler> stringLocalizer)
            : base(httpContextAccessor, clock)
        {
            S = stringLocalizer;
        }

        /// <inheritdoc/>
        public override Task ValidatingAsync(ValidatingContext<RecurringPlan> context, CancellationToken cancellationToken = default)
        {
            var plan = context.Model;

            if (string.IsNullOrWhiteSpace(plan.Name))
            {
                context.Result.Fail(new ValidationResult(S["Give the plan a name."], [nameof(RecurringPlan.Name)]));
            }

            if (string.IsNullOrWhiteSpace(plan.Rule) || !RecurrenceRule.TryParse(plan.Rule, out _, out var error))
            {
                context.Result.Fail(new ValidationResult(S["The schedule is not valid."], [nameof(RecurringPlan.Rule)]));
            }

            if (plan.EndDate is { } end && end < plan.StartDate)
            {
                context.Result.Fail(new ValidationResult(S["The plan can't end before it starts."], [nameof(RecurringPlan.EndDate)]));
            }

            if (plan.MaxRuns is < 1)
            {
                context.Result.Fail(new ValidationResult(S["The number of runs must be at least 1."], [nameof(RecurringPlan.MaxRuns)]));
            }

            if (plan.PriceIncreasePercent is < 0m or > 100m || (plan.PriceIncreasePercent is > 0m && plan.PriceIncreaseEveryRuns is not > 0))
            {
                context.Result.Fail(new ValidationResult(S["A price rise needs a percent from 0 to 100 and how many runs pass between rises."], [nameof(RecurringPlan.PriceIncreasePercent)]));
            }

            foreach (var issue in InvoiceIssueRules.ValidateLines(plan))
            {
                context.Result.Fail(new ValidationResult(InvoiceIssueMessages.Describe(issue, plan, S), [nameof(RecurringPlan.Lines)]));
            }

            foreach (var charge in plan.UsageCharges ?? [])
            {
                if (string.IsNullOrEmpty(charge?.MeterId))
                {
                    context.Result.Fail(new ValidationResult(S["Choose the meter of each usage charge."], [nameof(RecurringPlan.UsageCharges)]));

                    continue;
                }

                foreach (var tierError in UsagePricing.Validate(charge.Tiers, S).Distinct())
                {
                    context.Result.Fail(new ValidationResult(tierError, [nameof(RecurringPlan.UsageCharges)]));
                }
            }

            return Task.CompletedTask;
        }
    }
}
