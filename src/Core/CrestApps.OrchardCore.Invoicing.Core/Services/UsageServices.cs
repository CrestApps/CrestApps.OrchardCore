using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Invoicing.Core.Indexes
{
    /// <summary>
    /// Indexes a usage meter by name, code and whether it takes usage.
    /// </summary>
    public sealed class UsageMeterIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
    {
        public string Name { get; set; }

        public string Code { get; set; }

        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Maps <see cref="UsageMeter"/> to <see cref="UsageMeterIndex"/>.
    /// </summary>
    public sealed class UsageMeterIndexProvider : IndexProvider<UsageMeter>
    {
        public UsageMeterIndexProvider()
        {
            CollectionName = InvoicingConstants.CollectionName;
        }

        /// <inheritdoc/>
        public override void Describe(DescribeContext<UsageMeter> context)
        {
            context
                .For<UsageMeterIndex>()
                .Map(meter => new UsageMeterIndex
                {
                    ItemId = meter.ItemId,
                    Name = string.IsNullOrEmpty(meter.Name) || meter.Name.Length <= InvoiceIndexProvider.TextLength ? meter.Name : meter.Name[..InvoiceIndexProvider.TextLength],
                    Code = meter.Code,
                    Enabled = meter.Enabled,
                });
        }
    }

    /// <summary>
    /// Indexes a usage record by meter, customer, event id and when it happened, with its quantity so totals are added
    /// up from the index alone.
    /// </summary>
    public sealed class UsageRecordIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
    {
        public string MeterId { get; set; }

        public string PartyKind { get; set; }

        public string PartyId { get; set; }

        public string EventId { get; set; }

        public decimal Quantity { get; set; }

        public DateTime OccurredUtc { get; set; }

        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>
    /// Maps <see cref="UsageRecord"/> to <see cref="UsageRecordIndex"/>.
    /// </summary>
    public sealed class UsageRecordIndexProvider : IndexProvider<UsageRecord>
    {
        public UsageRecordIndexProvider()
        {
            CollectionName = InvoicingConstants.CollectionName;
        }

        /// <inheritdoc/>
        public override void Describe(DescribeContext<UsageRecord> context)
        {
            context
                .For<UsageRecordIndex>()
                .Map(record => new UsageRecordIndex
                {
                    ItemId = record.ItemId,
                    MeterId = record.MeterId,
                    PartyKind = record.Party.IsEmpty ? null : record.Party.Kind,
                    PartyId = record.Party.IsEmpty ? null : record.Party.Id,
                    EventId = record.EventId,
                    Quantity = record.Quantity,
                    OccurredUtc = record.OccurredUtc,
                    CreatedUtc = record.CreatedUtc,
                });
        }
    }
}

namespace CrestApps.OrchardCore.Invoicing.Core.Services
{
    /// <summary>
    /// The YesSql store of the usage meters.
    /// </summary>
    public interface IUsageMeterStore : ICatalog<UsageMeter>
    {
        /// <summary>
        /// Finds a meter by its code.
        /// </summary>
        Task<UsageMeter> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    }

    /// <inheritdoc cref="IUsageMeterStore"/>
    public sealed class UsageMeterStore : DocumentCatalog<UsageMeter, UsageMeterIndex>, IUsageMeterStore
    {
        public UsageMeterStore(ISession session)
            : base(session)
        {
            CollectionName = InvoicingConstants.CollectionName;
        }

        /// <inheritdoc/>
        public async Task<UsageMeter> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var normalized = code.Trim().ToLowerInvariant();

            return await Session.Query<UsageMeter, UsageMeterIndex>(index => index.Code == normalized, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
        }
    }

    /// <inheritdoc cref="IUsageMeterManager"/>
    public sealed class UsageMeterManager : CatalogManager<UsageMeter>, IUsageMeterManager
    {
        private readonly IUsageMeterStore _store;

        public UsageMeterManager(IUsageMeterStore store, IEnumerable<ICatalogEntryHandler<UsageMeter>> handlers, ILogger<CatalogManager<UsageMeter>> logger)
            : base(store, handlers, logger)
        {
            _store = store;
        }

        /// <inheritdoc/>
        public Task<UsageMeter> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
            => _store.FindByCodeAsync(code, cancellationToken);
    }

    /// <summary>
    /// Refuses a meter without a name or with a code that is not valid or taken, and keeps codes in lower case.
    /// </summary>
    public sealed partial class UsageMeterHandler : CatalogEntryHandlerBase<UsageMeter>
    {
        private readonly IUsageMeterStore _store;

        internal readonly IStringLocalizer S;

        public UsageMeterHandler(IUsageMeterStore store, IStringLocalizer<UsageMeterHandler> stringLocalizer)
        {
            _store = store;
            S = stringLocalizer;
        }

        /// <inheritdoc/>
        public override async Task ValidatingAsync(ValidatingContext<UsageMeter> context, CancellationToken cancellationToken = default)
        {
            var meter = context.Model;

            meter.Code = meter.Code?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(meter.Name))
            {
                context.Result.Fail(new ValidationResult(S["Give the meter a name."], [nameof(UsageMeter.Name)]));
            }

            if (string.IsNullOrEmpty(meter.Code) || meter.Code.Length > 64 || !CodePattern().IsMatch(meter.Code))
            {
                context.Result.Fail(new ValidationResult(S["The code must be 1 to 64 lowercase letters, digits, dots, dashes or underscores, such as api-calls."], [nameof(UsageMeter.Code)]));
            }
            else if (await _store.FindByCodeAsync(meter.Code, cancellationToken) is { } other && other.ItemId != meter.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["Another meter has the code '{0}'.", meter.Code], [nameof(UsageMeter.Code)]));
            }
        }

        [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$")]
        private static partial Regex CodePattern();
    }

    /// <summary>
    /// The default <see cref="IUsageService"/>: a record per event, kept once per meter and event id under a lock, and
    /// totals added up from the index.
    /// </summary>
    public sealed class DefaultUsageService : IUsageService
    {
        private const string LockPrefix = "Invoicing:Usage:";

        private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

        private readonly IUsageMeterStore _meters;
        private readonly ISession _session;
        private readonly IDistributedLock _distributedLock;
        private readonly IClock _clock;

        internal readonly IStringLocalizer S;

        public DefaultUsageService(
            IUsageMeterStore meters,
            ISession session,
            IDistributedLock distributedLock,
            IClock clock,
            IStringLocalizer<DefaultUsageService> stringLocalizer)
        {
            _meters = meters;
            _session = session;
            _distributedLock = distributedLock;
            _clock = clock;
            S = stringLocalizer;
        }

        /// <inheritdoc/>
        public async Task<UsageRecordResult> RecordAsync(UsageRecordRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var result = new UsageRecordResult();
            var meter = await FindMeterAsync(request.Meter, cancellationToken);

            if (meter is null)
            {
                return result.Fail(S["The meter '{0}' could not be found.", request.Meter ?? string.Empty]);
            }

            if (!meter.Enabled)
            {
                return result.Fail(S["The meter '{0}' is turned off and takes no usage.", meter.Name]);
            }

            if (request.Party.IsEmpty)
            {
                return result.Fail(S["Say whose usage it is."]);
            }

            if (request.Quantity < 0m)
            {
                return result.Fail(S["The quantity can't be negative."]);
            }

            var eventId = string.IsNullOrWhiteSpace(request.EventId) ? IdGenerator.GenerateId() : request.EventId.Trim();

            if (eventId.Length > InvoiceIndexProvider.IdLength * 2)
            {
                return result.Fail(S["The event id can be at most {0} characters.", InvoiceIndexProvider.IdLength * 2]);
            }

            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + meter.ItemId + ":" + eventId, _lockTimeout, _lockExpiration);

            if (!locked)
            {
                return result.Fail(S["This usage is being recorded already. Try again in a moment."]);
            }

            await using var _ = locker;

            var meterId = meter.ItemId;
            var existing = await _session.Query<UsageRecord, UsageRecordIndex>(index => index.MeterId == meterId && index.EventId == eventId, collection: InvoicingConstants.CollectionName)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                result.Record = existing;
                result.AlreadyRecorded = true;

                return result;
            }

            var now = _clock.UtcNow;
            var record = new UsageRecord
            {
                ItemId = IdGenerator.GenerateId(),
                MeterId = meter.ItemId,
                Party = request.Party,
                Quantity = request.Quantity,
                OccurredUtc = DateTime.SpecifyKind(request.OccurredUtc ?? now, DateTimeKind.Utc),
                EventId = eventId,
                CreatedUtc = now,
            };

            await _session.SaveAsync(record, collection: InvoicingConstants.CollectionName, cancellationToken: cancellationToken);

            // Written before the lock is let go, so a second call for the event finds it.
            await _session.SaveChangesAsync(cancellationToken);

            result.Record = record;

            return result;
        }

        /// <inheritdoc/>
        public async Task<decimal> GetTotalAsync(string meterId, PartyRef party, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(meterId) || party.IsEmpty || toUtc <= fromUtc)
            {
                return 0m;
            }

            var meter = await _meters.FindByIdAsync(meterId, cancellationToken);

            if (meter is null)
            {
                return 0m;
            }

            var kind = party.Kind;
            var id = party.Id;
            var rows = await _session.QueryIndex<UsageRecordIndex>(index => index.MeterId == meterId && index.PartyKind == kind && index.PartyId == id && index.OccurredUtc >= fromUtc && index.OccurredUtc < toUtc, collection: InvoicingConstants.CollectionName)
                .ListAsync(cancellationToken);
            var records = rows.ToList();

            if (records.Count == 0)
            {
                return 0m;
            }

            return meter.Aggregation switch
            {
                UsageAggregation.Maximum => records.Max(row => row.Quantity),
                UsageAggregation.Last => records.OrderBy(row => row.OccurredUtc).ThenBy(row => row.CreatedUtc).Last().Quantity,
                _ => records.Sum(row => row.Quantity),
            };
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<UsageRecord>> ListRecentAsync(string meterId, int count, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(meterId) || count <= 0)
            {
                return [];
            }

            var records = await _session.Query<UsageRecord, UsageRecordIndex>(index => index.MeterId == meterId, collection: InvoicingConstants.CollectionName)
                .OrderByDescending(index => index.OccurredUtc)
                .ThenByDescending(index => index.CreatedUtc)
                .Take(Math.Min(count, 500))
                .ListAsync(cancellationToken);

            return [.. records];
        }

        private async Task<UsageMeter> FindMeterAsync(string meter, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(meter))
            {
                return null;
            }

            return await _meters.FindByCodeAsync(meter, cancellationToken) ?? await _meters.FindByIdAsync(meter.Trim(), cancellationToken);
        }
    }

    /// <summary>
    /// Prices a quantity by tiers: graduated, each band at its own tier's price, or volume, all of it at the price of the
    /// tier it falls in.
    /// </summary>
    public static class UsagePricing
    {
        /// <summary>
        /// The bands a quantity is billed in: one per tier it reaches when graduated, or one when by volume. Nothing for
        /// no usage.
        /// </summary>
        public static IReadOnlyList<UsageBand> Price(UsageTierMode mode, IEnumerable<UsagePriceTier> tiers, decimal quantity)
        {
            var ordered = Order(tiers);

            if (quantity <= 0m || ordered.Count == 0)
            {
                return [];
            }

            if (mode == UsageTierMode.Volume)
            {
                var tier = ordered.FirstOrDefault(item => item.UpTo is not { } upTo || quantity <= upTo) ?? ordered[^1];

                return [new UsageBand(0m, tier.UpTo, quantity, tier.UnitPrice)];
            }

            var bands = new List<UsageBand>();
            var from = 0m;

            foreach (var tier in ordered)
            {
                if (quantity <= from)
                {
                    break;
                }

                var to = tier.UpTo is { } upTo ? Math.Min(upTo, quantity) : quantity;

                if (to > from)
                {
                    bands.Add(new UsageBand(from, tier.UpTo, to - from, tier.UnitPrice));
                }

                from = tier.UpTo ?? quantity;
            }

            // The last tier set a limit the usage went past: the rest is at its price.
            if (quantity > from && ordered[^1].UpTo is not null)
            {
                bands.Add(new UsageBand(from, null, quantity - from, ordered[^1].UnitPrice));
            }

            return bands;
        }

        /// <summary>
        /// What is wrong with a set of tiers, or nothing: at least one, prices not negative, limits above zero and rising,
        /// and only the last without a limit.
        /// </summary>
        public static IReadOnlyList<string> Validate(IList<UsagePriceTier> tiers, IStringLocalizer S)
        {
            var errors = new List<string>();

            if (tiers is null || tiers.Count == 0)
            {
                errors.Add(S["Give the usage charge at least one price tier."]);

                return errors;
            }

            decimal? previous = 0m;

            for (var i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];

                if (tier.UnitPrice < 0m)
                {
                    errors.Add(S["A tier's price can't be negative."]);
                }

                if (tier.UpTo is null && i < tiers.Count - 1)
                {
                    errors.Add(S["Only the last tier can go without an upper limit."]);
                }
                else if (tier.UpTo is { } upTo && (upTo <= 0m || upTo <= previous))
                {
                    errors.Add(S["Each tier's upper limit must be above the one before it."]);
                }

                previous = tier.UpTo;
            }

            return errors;
        }

        /// <summary>
        /// Names a band for an invoice line, such as <c>API calls (1,000 – 5,000)</c>; a single band keeps the name.
        /// </summary>
        public static string Describe(string name, UsageBand band, int bandCount, CultureInfo culture)
        {
            if (bandCount <= 1)
            {
                return name;
            }

            return band.UpTo is { } upTo
                ? $"{name} ({band.From.ToString("#,0.##", culture)} – {upTo.ToString("#,0.##", culture)})"
                : $"{name} ({band.From.ToString("#,0.##", culture)}+)";
        }

        private static List<UsagePriceTier> Order(IEnumerable<UsagePriceTier> tiers)
            => [.. (tiers ?? []).Where(tier => tier is not null).OrderBy(tier => tier.UpTo ?? decimal.MaxValue)];
    }

    /// <summary>
    /// A band of usage billed at one price: from a quantity up to a tier's limit (none for the last).
    /// </summary>
    public sealed record UsageBand(decimal From, decimal? UpTo, decimal Quantity, decimal UnitPrice);
}
