using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Invoicing.Core.Indexes
{
    /// <summary>
    /// Indexes a late-fee policy by name and whether it charges fees.
    /// </summary>
    public sealed class LateFeePolicyIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
    {
        /// <summary>
        /// The policy's name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether the policy charges fees.
        /// </summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Maps <see cref="LateFeePolicy"/> to <see cref="LateFeePolicyIndex"/>.
    /// </summary>
    public sealed class LateFeePolicyIndexProvider : IndexProvider<LateFeePolicy>
    {
        public LateFeePolicyIndexProvider()
        {
            CollectionName = InvoicingConstants.CollectionName;
        }

        /// <inheritdoc/>
        public override void Describe(DescribeContext<LateFeePolicy> context)
        {
            context
                .For<LateFeePolicyIndex>()
                .Map(policy => new LateFeePolicyIndex
                {
                    ItemId = policy.ItemId,
                    Name = string.IsNullOrEmpty(policy.Name) || policy.Name.Length <= InvoiceIndexProvider.TextLength ? policy.Name : policy.Name[..InvoiceIndexProvider.TextLength],
                    Enabled = policy.Enabled,
                });
        }
    }
}

namespace CrestApps.OrchardCore.Invoicing.Core.Services
{
    /// <summary>
    /// The YesSql store of the late-fee policies.
    /// </summary>
    public interface ILateFeePolicyStore : ICatalog<LateFeePolicy>
    {
        /// <summary>
        /// Gets the policies that charge fees, by name.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        Task<IReadOnlyList<LateFeePolicy>> GetEnabledAsync(CancellationToken cancellationToken = default);
    }

    /// <inheritdoc cref="ILateFeePolicyStore"/>
    public sealed class LateFeePolicyStore : DocumentCatalog<LateFeePolicy, LateFeePolicyIndex>, ILateFeePolicyStore
    {
        public LateFeePolicyStore(ISession session)
            : base(session)
        {
            CollectionName = InvoicingConstants.CollectionName;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<LateFeePolicy>> GetEnabledAsync(CancellationToken cancellationToken = default)
        {
            var policies = await Session.Query<LateFeePolicy, LateFeePolicyIndex>(index => index.Enabled, collection: CollectionName)
                .OrderBy(index => index.Name)
                .ListAsync(cancellationToken);

            return [.. policies];
        }
    }

    /// <inheritdoc cref="ILateFeePolicyManager"/>
    public sealed class LateFeePolicyManager : CatalogManager<LateFeePolicy>, ILateFeePolicyManager
    {
        private readonly ILateFeePolicyStore _store;

        public LateFeePolicyManager(ILateFeePolicyStore store, IEnumerable<ICatalogEntryHandler<LateFeePolicy>> handlers, ILogger<CatalogManager<LateFeePolicy>> logger)
            : base(store, handlers, logger)
        {
            _store = store;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<LateFeePolicy>> GetEnabledAsync(CancellationToken cancellationToken = default)
            => _store.GetEnabledAsync(cancellationToken);
    }

    /// <summary>
    /// Refuses a late-fee policy that could not charge a sensible fee.
    /// </summary>
    public sealed class LateFeePolicyHandler : CatalogEntryHandlerBase<LateFeePolicy>
    {
        internal readonly IStringLocalizer S;

        public LateFeePolicyHandler(IStringLocalizer<LateFeePolicyHandler> stringLocalizer)
        {
            S = stringLocalizer;
        }

        /// <inheritdoc/>
        public override Task ValidatingAsync(ValidatingContext<LateFeePolicy> context, CancellationToken cancellationToken = default)
        {
            var policy = context.Model;

            if (string.IsNullOrWhiteSpace(policy.Name))
            {
                context.Result.Fail(new ValidationResult(S["Give the policy a name."], [nameof(LateFeePolicy.Name)]));
            }

            if (policy.Kind == LateFeeKind.Flat && policy.Amount <= 0m)
            {
                context.Result.Fail(new ValidationResult(S["A fixed fee needs an amount above zero."], [nameof(LateFeePolicy.Amount)]));
            }

            if (policy.Kind == LateFeeKind.Percent && policy.Percent is <= 0m or > 100m)
            {
                context.Result.Fail(new ValidationResult(S["A percent fee needs a percent above zero and at most 100."], [nameof(LateFeePolicy.Percent)]));
            }

            if (policy.GraceDays < 0 || policy.GraceDays > 365)
            {
                context.Result.Fail(new ValidationResult(S["The grace days must be from 0 to 365."], [nameof(LateFeePolicy.GraceDays)]));
            }

            if (policy.MaxFees is < 1)
            {
                context.Result.Fail(new ValidationResult(S["The number of fees must be at least 1."], [nameof(LateFeePolicy.MaxFees)]));
            }

            if (policy.CapAmount is <= 0m || policy.MinimumBalance < 0m)
            {
                context.Result.Fail(new ValidationResult(S["The cap must be above zero, and the minimum balance can't be negative."], [nameof(LateFeePolicy.CapAmount)]));
            }

            return Task.CompletedTask;
        }
    }
}
