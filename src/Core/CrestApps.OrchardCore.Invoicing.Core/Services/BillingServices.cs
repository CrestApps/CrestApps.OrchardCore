using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Invoicing.Core.Indexes
{
    /// <summary>
    /// Indexes a billable charge by customer, currency, day and whether an invoice bills it.
    /// </summary>
    public sealed class BillableItemIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
    {
        public string PartyKind { get; set; }

        public string PartyId { get; set; }

        public string PartyName { get; set; }

        public string Description { get; set; }

        public string Currency { get; set; }

        public DateTime Date { get; set; }

        public string InvoiceId { get; set; }

        public bool IsBilled { get; set; }

        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>
    /// Maps <see cref="BillableItem"/> to <see cref="BillableItemIndex"/>.
    /// </summary>
    public sealed class BillableItemIndexProvider : IndexProvider<BillableItem>
    {
        public BillableItemIndexProvider()
        {
            CollectionName = InvoicingConstants.CollectionName;
        }

        /// <inheritdoc/>
        public override void Describe(DescribeContext<BillableItem> context)
        {
            context
                .For<BillableItemIndex>()
                .Map(item => new BillableItemIndex
                {
                    ItemId = item.ItemId,
                    PartyKind = item.Party.IsEmpty ? null : item.Party.Kind,
                    PartyId = item.Party.IsEmpty ? null : item.Party.Id,
                    PartyName = Truncate(item.PartyName),
                    Description = Truncate(item.Description),
                    Currency = item.Currency,
                    Date = item.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    InvoiceId = item.InvoiceId,
                    IsBilled = item.IsBilled,
                    CreatedUtc = item.CreatedUtc,
                });
        }

        private static string Truncate(string value)
            => string.IsNullOrEmpty(value) || value.Length <= InvoiceIndexProvider.TextLength ? value : value[..InvoiceIndexProvider.TextLength];
    }
}

namespace CrestApps.OrchardCore.Invoicing.Core.Services
{
    /// <summary>
    /// The YesSql store of the billable charges.
    /// </summary>
    public interface IBillableItemStore : ICatalog<BillableItem>
    {
        /// <summary>
        /// Lists a customer's unbilled charges in a currency up to a day, oldest first.
        /// </summary>
        Task<IReadOnlyList<BillableItem>> ListUnbilledAsync(Parties.Models.PartyRef party, string currency, DateOnly? through, CancellationToken cancellationToken = default);
    }

    /// <inheritdoc cref="IBillableItemStore"/>
    public sealed class BillableItemStore : DocumentCatalog<BillableItem, BillableItemIndex>, IBillableItemStore
    {
        public BillableItemStore(ISession session)
            : base(session)
        {
            CollectionName = InvoicingConstants.CollectionName;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<BillableItem>> ListUnbilledAsync(Parties.Models.PartyRef party, string currency, DateOnly? through, CancellationToken cancellationToken = default)
        {
            if (party.IsEmpty)
            {
                return [];
            }

            var kind = party.Kind;
            var id = party.Id;
            var last = (through ?? DateOnly.MaxValue.AddDays(-1)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var query = Session.Query<BillableItem, BillableItemIndex>(index => index.PartyKind == kind && index.PartyId == id && !index.IsBilled && index.Date <= last, collection: CollectionName);

            if (!string.IsNullOrEmpty(currency))
            {
                query = query.Where(index => index.Currency == currency);
            }

            return [.. await query.OrderBy(index => index.Date).ThenBy(index => index.CreatedUtc).ListAsync(cancellationToken)];
        }
    }

    /// <inheritdoc cref="IBillableItemManager"/>
    public sealed class BillableItemManager : CatalogManager<BillableItem>, IBillableItemManager
    {
        public BillableItemManager(IBillableItemStore store, IEnumerable<ICatalogEntryHandler<BillableItem>> handlers, ILogger<CatalogManager<BillableItem>> logger)
            : base(store, handlers, logger)
        {
        }
    }

    /// <summary>
    /// Stamps a new charge and refuses one that could not be billed, or a change to one an invoice bills.
    /// </summary>
    public sealed class BillableItemHandler : CatalogEntryHandlerBase<BillableItem>
    {
        private readonly IClock _clock;

        internal readonly IStringLocalizer S;

        public BillableItemHandler(IClock clock, IStringLocalizer<BillableItemHandler> stringLocalizer)
        {
            _clock = clock;
            S = stringLocalizer;
        }

        /// <inheritdoc/>
        public override Task InitializedAsync(InitializedContext<BillableItem> context, CancellationToken cancellationToken = default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task ValidatingAsync(ValidatingContext<BillableItem> context, CancellationToken cancellationToken = default)
        {
            var item = context.Model;

            if (item.Party.IsEmpty)
            {
                context.Result.Fail(new ValidationResult(S["Choose the customer the charge is billed to."], [nameof(BillableItem.Party)]));
            }

            if (string.IsNullOrWhiteSpace(item.Description))
            {
                context.Result.Fail(new ValidationResult(S["Describe the charge."], [nameof(BillableItem.Description)]));
            }

            if (item.Quantity <= 0m || item.UnitCost < 0m || item.MarkupPercent < 0m)
            {
                context.Result.Fail(new ValidationResult(S["The quantity must be above zero, and the cost and markup can't be negative."], [nameof(BillableItem.Quantity)]));
            }

            if (string.IsNullOrWhiteSpace(item.Currency) || item.Currency.Trim().Length != 3)
            {
                context.Result.Fail(new ValidationResult(S["The currency must be a three-letter code, such as USD."], [nameof(BillableItem.Currency)]));
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The charges staff and systems keep to bill later (<see cref="BillableItem"/>), as a billable source.
    /// </summary>
    public sealed class ChargesBillableSource : IBillableSource
    {
        public const string SourceType = "charges";

        private readonly IBillableItemStore _store;

        internal readonly IStringLocalizer S;

        public ChargesBillableSource(IBillableItemStore store, IStringLocalizer<ChargesBillableSource> stringLocalizer)
        {
            _store = store;
            S = stringLocalizer;
        }

        /// <inheritdoc/>
        public string Type => SourceType;

        /// <inheritdoc/>
        public async Task<IReadOnlyList<BillableCandidate>> ListUnbilledAsync(BillableQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var items = await _store.ListUnbilledAsync(query.Party, query.Currency, query.Through, cancellationToken);

            return [.. items.Where(item => query.From is null || item.Date >= query.From).Select(item => new BillableCandidate
            {
                Source = SourceType,
                Id = item.ItemId,
                Date = item.Date,
                Kind = item.Kind,
                Description = item.Description,
                Quantity = item.Quantity,
                Unit = item.Unit,
                UnitPrice = item.UnitPrice,
                Currency = item.Currency,
                TaxCategoryCode = item.TaxCategoryCode,
            })];
        }

        /// <inheritdoc/>
        public async Task<string> MarkBilledAsync(string id, string invoiceId, string lineId, CancellationToken cancellationToken = default)
        {
            var item = await _store.FindByIdAsync(id, cancellationToken);

            if (item is null)
            {
                return S["The charge no longer exists."];
            }

            if (item.IsBilled && !string.Equals(item.InvoiceId, invoiceId, StringComparison.Ordinal))
            {
                return S["The charge is billed on another invoice already."];
            }

            item.InvoiceId = invoiceId;
            item.InvoiceLineId = lineId;
            await _store.UpdateAsync(item, cancellationToken);

            return null;
        }

        /// <inheritdoc/>
        public async Task ReleaseAsync(string id, CancellationToken cancellationToken = default)
        {
            if (await _store.FindByIdAsync(id, cancellationToken) is { IsBilled: true } item)
            {
                item.InvoiceId = null;
                item.InvoiceLineId = null;
                await _store.UpdateAsync(item, cancellationToken);
            }
        }
    }

    /// <summary>
    /// The default <see cref="IBillingService"/>.
    /// </summary>
    public sealed class DefaultBillingService : IBillingService
    {
        /// <summary>
        /// The prefix of the source type of a line that bills an item of a billable source.
        /// </summary>
        public const string LineSourcePrefix = "billable:";

        private readonly IEnumerable<IBillableSource> _sources;
        private readonly IInvoiceManager _invoices;
        private readonly IDocumentCalculator _calculator;
        private readonly ISiteService _siteService;

        internal readonly IStringLocalizer S;

        public DefaultBillingService(
            IEnumerable<IBillableSource> sources,
            IInvoiceManager invoices,
            IDocumentCalculator calculator,
            ISiteService siteService,
            IStringLocalizer<DefaultBillingService> stringLocalizer)
        {
            _sources = sources;
            _invoices = invoices;
            _calculator = calculator;
            _siteService = siteService;
            S = stringLocalizer;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<BillableCandidate>> ListUnbilledAsync(string source, BillableQuery query, CancellationToken cancellationToken = default)
            => Find(source) is { } found
                ? found.ListUnbilledAsync(query, cancellationToken)
                : Task.FromResult<IReadOnlyList<BillableCandidate>>([]);

        /// <inheritdoc/>
        public async Task<InvoiceCommandResult> AddToInvoiceAsync(string invoiceId, string source, IEnumerable<string> ids, BillableQuery query, CancellationToken cancellationToken = default)
        {
            var result = new InvoiceCommandResult();
            var invoice = string.IsNullOrEmpty(invoiceId) ? null : await _invoices.FindByIdAsync(invoiceId, cancellationToken);

            if (invoice is null)
            {
                return result.Fail(S["The invoice could not be found."]);
            }

            result.Invoice = invoice;

            if (invoice.Status != InvoiceStatus.Draft)
            {
                return result.Fail(S["Work can only be added to a draft invoice."]);
            }

            query ??= new BillableQuery();
            query.Party = invoice.BillTo?.Party ?? default;
            query.Currency = invoice.Currency;

            var chosen = (ids ?? []).ToHashSet(StringComparer.Ordinal);
            var candidates = (await ListUnbilledAsync(source, query, cancellationToken)).Where(candidate => chosen.Contains(candidate.Id)).ToList();

            if (candidates.Count == 0)
            {
                return result.Fail(S["Nothing chosen is unbilled any more."]);
            }

            await AddToDraftAsync(invoice, source, candidates, cancellationToken);
            await InvoiceCalculation.ApplyAsync(_calculator, invoice, await _siteService.GetSettingsAsync<InvoicingSettings>(), issueDate: null, cancellationToken);
            await _invoices.UpdateAsync(invoice, cancellationToken: cancellationToken);

            return result;
        }

        /// <inheritdoc/>
        public async Task<int> AddToDraftAsync(Invoice invoice, string source, IEnumerable<BillableCandidate> candidates, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invoice);

            if (Find(source) is not { } found)
            {
                return 0;
            }

            // An invoice being built has no id until it is saved; the items need one to name it.
            if (string.IsNullOrEmpty(invoice.ItemId))
            {
                invoice.ItemId = IdGenerator.GenerateId();
            }

            var added = 0;

            foreach (var candidate in candidates ?? [])
            {
                var lineId = IdGenerator.GenerateId();

                if (await found.MarkBilledAsync(candidate.Id, invoice.ItemId, lineId, cancellationToken) is not null)
                {
                    continue;
                }

                invoice.Lines.Add(new DocumentLine
                {
                    Id = lineId,
                    Kind = candidate.Kind,
                    Name = candidate.Description,
                    Quantity = candidate.Quantity,
                    Unit = candidate.Unit,
                    UnitPrice = candidate.UnitPrice,
                    TaxCategoryCode = candidate.TaxCategoryCode,
                    ServiceStart = candidate.Date,
                    ServiceEnd = candidate.Date,
                    SourceType = LineSourcePrefix + found.Type,
                    SourceId = candidate.Id,
                });
                invoice.BilledItems.Add(new InvoiceBilledItem { Source = found.Type, Id = candidate.Id, LineId = lineId });
                added++;
            }

            return added;
        }

        /// <inheritdoc/>
        public async Task ReconcileAsync(Invoice invoice, bool releaseAll = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invoice);

            var lineIds = invoice.Lines.Select(line => line.Id).ToHashSet(StringComparer.Ordinal);

            foreach (var billed in invoice.BilledItems.ToList())
            {
                if (!releaseAll && lineIds.Contains(billed.LineId))
                {
                    continue;
                }

                if (Find(billed.Source) is { } source)
                {
                    await source.ReleaseAsync(billed.Id, cancellationToken);
                }

                if (!releaseAll)
                {
                    invoice.BilledItems.Remove(billed);
                }
            }
        }

        /// <inheritdoc/>
        public async Task ReleaseLinesAsync(IEnumerable<DocumentLine> lines, CancellationToken cancellationToken = default)
        {
            foreach (var line in lines ?? [])
            {
                if (line.SourceType?.StartsWith(LineSourcePrefix, StringComparison.Ordinal) == true &&
                    !string.IsNullOrEmpty(line.SourceId) &&
                    Find(line.SourceType[LineSourcePrefix.Length..]) is { } source)
                {
                    await source.ReleaseAsync(line.SourceId, cancellationToken);
                }
            }
        }

        private IBillableSource Find(string type)
            => string.IsNullOrEmpty(type) ? null : _sources.FirstOrDefault(source => string.Equals(source.Type, type, StringComparison.Ordinal));
    }

    /// <summary>
    /// Keeps the billed state of billable work in step with the invoices that bill it: a draft saved without a line lets
    /// its item go, a draft deleted or an invoice voided lets all of them go, and a credit note that credits all of a line
    /// lets its item go.
    /// </summary>
    public sealed class BillingInvoiceHandler : CatalogEntryHandlerBase<Invoice>, IInvoiceObserver
    {
        private readonly IServiceProvider _serviceProvider;

        public BillingInvoiceHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        // Resolved when needed: the billing service uses the invoice manager, whose handlers include this one.
        private IBillingService _billing
            => _serviceProvider.GetRequiredService<IBillingService>();

        /// <inheritdoc/>
        public override Task UpdatingAsync(UpdatingContext<Invoice> context, CancellationToken cancellationToken = default)
            => context.Model is { Status: InvoiceStatus.Draft, BilledItems.Count: > 0 } draft
                ? _billing.ReconcileAsync(draft, releaseAll: false, cancellationToken)
                : Task.CompletedTask;

        /// <inheritdoc/>
        public override Task DeletedAsync(DeletedContext<Invoice> context, CancellationToken cancellationToken = default)
            => context.Model is { BilledItems.Count: > 0 } invoice
                ? _billing.ReconcileAsync(invoice, releaseAll: true, cancellationToken)
                : Task.CompletedTask;

        /// <inheritdoc/>
        public Task VoidedAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => invoice is { BilledItems.Count: > 0 }
                ? _billing.ReconcileAsync(invoice, releaseAll: true, cancellationToken)
                : Task.CompletedTask;

        /// <inheritdoc/>
        public async Task CreditNoteIssuedAsync(CreditNote creditNote, Invoice invoice, CancellationToken cancellationToken = default)
        {
            if (creditNote is null || invoice is not { BilledItems.Count: > 0 })
            {
                return;
            }

            // A line credited in full no longer bills its item.
            var credited = creditNote.Lines
                .Where(line => !string.IsNullOrEmpty(line.CreditedLineId))
                .GroupBy(line => line.CreditedLineId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity), StringComparer.Ordinal);

            var fully = invoice.Lines.Where(line => credited.TryGetValue(line.Id, out var quantity) && quantity >= line.Quantity).ToList();

            await _billing.ReleaseLinesAsync(fully, cancellationToken);
        }
    }
}
