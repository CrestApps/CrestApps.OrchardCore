using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The shape of the <c>Transactions</c> recipe step, which carries the ledger between sites: the transactions with their
/// timelines, and the payment attempts, refunds and payment issues.
/// </summary>
public static class TransactionLedgerStep
{
    /// <summary>
    /// The name of the recipe step.
    /// </summary>
    public const string Name = "Transactions";

    /// <summary>
    /// The array of transactions.
    /// </summary>
    public const string Transactions = "Transactions";

    /// <summary>
    /// The array of payment attempts.
    /// </summary>
    public const string PaymentAttempts = "PaymentAttempts";

    /// <summary>
    /// The array of refunds.
    /// </summary>
    public const string PaymentRefunds = "PaymentRefunds";

    /// <summary>
    /// The array of payment issues.
    /// </summary>
    public const string PaymentIssues = "PaymentIssues";

    /// <summary>
    /// Writes a ledger record with everything it holds but the storing site's document id: unlike configuration, a
    /// ledger record keeps when it was made and who made it.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject Export(object record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var node = JsonSerializer.SerializeToNode(record, record.GetType(), CatalogDeploymentSerializer.Options)?.AsObject() ?? [];

        node.Remove("Id");

        return node;
    }
}

/// <summary>
/// Writes the <c>Transactions</c> recipe step: the whole ledger, or the transactions a caller names with what was paid
/// and refunded on them.
/// </summary>
public sealed class TransactionLedgerExporter
{
    private const int PageSize = 500;

    private readonly ITransactionStore _transactions;
    private readonly IServiceProvider _serviceProvider;

    public TransactionLedgerExporter(ITransactionStore transactions, IServiceProvider serviceProvider)
    {
        _transactions = transactions;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Writes every transaction, payment attempt, refund and payment issue the site keeps. The payment attempts and
    /// refunds are kept by Checkout and are left out without it.
    /// </summary>
    public async Task<JsonObject> ExportAllAsync(CancellationToken cancellationToken = default)
    {
        var transactions = new JsonArray();

        for (var page = 1; ; page++)
        {
            var result = await _transactions.PageAsync(page, PageSize, new TransactionQuery(), cancellationToken);
            var entries = result.Entries?.ToArray() ?? [];

            foreach (var transaction in entries)
            {
                transactions.Add(TransactionLedgerStep.Export(transaction));
            }

            if (entries.Length < PageSize)
            {
                break;
            }
        }

        var step = new JsonObject
        {
            ["name"] = TransactionLedgerStep.Name,
            [TransactionLedgerStep.Transactions] = transactions,
        };

        if (_serviceProvider.GetService<IPaymentAttemptStore>() is { } attempts)
        {
            step[TransactionLedgerStep.PaymentAttempts] = await PageAllAsync(page => attempts.PageAsync(page, PageSize, new PaymentAttemptQuery(), cancellationToken));
        }

        if (_serviceProvider.GetService<IPaymentRefundStore>() is { } refunds)
        {
            step[TransactionLedgerStep.PaymentRefunds] = await PageAllAsync(page => refunds.PageAsync(page, PageSize, new PaymentRefundQuery(), cancellationToken));
        }

        if (_serviceProvider.GetService<IPaymentIssueStore>() is { } issues)
        {
            step[TransactionLedgerStep.PaymentIssues] = await PageAllAsync(page => issues.PageAsync(page, PageSize, null, cancellationToken));
        }

        return step;
    }

    /// <summary>
    /// Writes the named transactions, the payment attempts made toward them and the refunds of those attempts. An id no
    /// transaction has is skipped.
    /// </summary>
    public async Task<JsonObject> ExportAsync(IEnumerable<string> transactionIds, CancellationToken cancellationToken = default)
    {
        var transactions = new JsonArray();
        var attempts = new JsonArray();
        var refunds = new JsonArray();
        var attemptStore = _serviceProvider.GetService<IPaymentAttemptStore>();
        var refundStore = _serviceProvider.GetService<IPaymentRefundStore>();

        foreach (var id in (transactionIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (await _transactions.FindByIdAsync(id, cancellationToken) is not { } transaction)
            {
                continue;
            }

            transactions.Add(TransactionLedgerStep.Export(transaction));

            if (attemptStore is null)
            {
                continue;
            }

            foreach (var attempt in await attemptStore.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, id, cancellationToken))
            {
                attempts.Add(TransactionLedgerStep.Export(attempt));

                if (refundStore is null || string.IsNullOrEmpty(attempt.SessionId))
                {
                    continue;
                }

                foreach (var refund in (await refundStore.GetBySessionAsync(attempt.SessionId, cancellationToken))
                    .Where(refund => string.Equals(refund.OriginalAttemptId, attempt.ItemId, StringComparison.Ordinal)))
                {
                    refunds.Add(TransactionLedgerStep.Export(refund));
                }
            }
        }

        return new JsonObject
        {
            ["name"] = TransactionLedgerStep.Name,
            [TransactionLedgerStep.Transactions] = transactions,
            [TransactionLedgerStep.PaymentAttempts] = attempts,
            [TransactionLedgerStep.PaymentRefunds] = refunds,
        };
    }

    private static async Task<JsonArray> PageAllAsync<T>(Func<int, Task<CrestApps.Core.Models.PageResult<T>>> read)
    {
        var all = new JsonArray();

        for (var page = 1; ; page++)
        {
            var entries = (await read(page)).Entries?.ToArray() ?? [];

            foreach (var entry in entries)
            {
                all.Add(TransactionLedgerStep.Export(entry));
            }

            if (entries.Length < PageSize)
            {
                return all;
            }
        }
    }
}

/// <summary>
/// Imports the <c>Transactions</c> recipe step. Records are written to their stores as they are, never through the
/// ledger's commands: nothing is posted, numbered, announced to handlers, workflows or webhooks, or e-mailed. A record
/// the site already has is kept as it is, so a ledger is never rewritten by a recipe.
/// </summary>
public sealed class TransactionLedgerImporter
{
    private readonly ITransactionStore _transactions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public TransactionLedgerImporter(
        ITransactionStore transactions,
        IServiceProvider serviceProvider,
        ILogger<TransactionLedgerImporter> logger,
        IStringLocalizer<TransactionLedgerImporter> stringLocalizer)
    {
        _transactions = transactions;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Imports every array of the step.
    /// </summary>
    /// <param name="step">The recipe step.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was imported, kept and refused.</returns>
    public async Task<TransactionLedgerImportResult> ImportAsync(JsonObject step, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);

        var result = new TransactionLedgerImportResult();

        await ImportAsync(step, TransactionLedgerStep.Transactions, _transactions, S["transaction"], result, cancellationToken);
        await ImportOptionalAsync<PaymentAttempt, IPaymentAttemptStore>(step, TransactionLedgerStep.PaymentAttempts, S["payment attempt"], result, cancellationToken);
        await ImportOptionalAsync<PaymentRefund, IPaymentRefundStore>(step, TransactionLedgerStep.PaymentRefunds, S["refund"], result, cancellationToken);
        await ImportOptionalAsync<PaymentIssue, IPaymentIssueStore>(step, TransactionLedgerStep.PaymentIssues, S["payment issue"], result, cancellationToken);

        return result;
    }

    private async Task ImportOptionalAsync<T, TStore>(JsonObject step, string section, LocalizedString kind, TransactionLedgerImportResult result, CancellationToken cancellationToken)
        where T : CrestApps.Core.Models.CatalogItem
        where TStore : class, ICatalog<T>
    {
        if (step[section] is not JsonArray { Count: > 0 })
        {
            return;
        }

        if (_serviceProvider.GetService<TStore>() is not { } store)
        {
            result.Errors.Add(S["The recipe carries {0} records, which this site keeps only with the Checkout feature enabled. Enable it and import again.", kind].Value);

            return;
        }

        await ImportAsync(step, section, store, kind, result, cancellationToken);
    }

    private async Task ImportAsync<T>(JsonObject step, string section, ICatalog<T> store, LocalizedString kind, TransactionLedgerImportResult result, CancellationToken cancellationToken)
        where T : CrestApps.Core.Models.CatalogItem
    {
        foreach (var data in CatalogRecipeSections.Section(step, section))
        {
            var copy = (JsonObject)data.DeepClone();
            copy.Remove("Id");

            T record;

            try
            {
                record = copy.Deserialize<T>(CatalogDeploymentSerializer.Options);
            }
            catch (JsonException ex)
            {
                result.Errors.Add(S["A {0} could not be read: its value at {1} is not what it should be.", kind, ex.Path ?? "$"].Value);

                continue;
            }

            if (string.IsNullOrWhiteSpace(record?.ItemId))
            {
                result.Errors.Add(S["A {0} has no ItemId, so it was not imported.", kind].Value);

                continue;
            }

            if (await store.FindByIdAsync(record.ItemId, cancellationToken) is not null)
            {
                result.Kept++;

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("The {Kind} '{ItemId}' is already on this site, so the recipe left it as it is.", kind.Value, record.ItemId);
                }

                continue;
            }

            // The store stamps the time it saves; a ledger record keeps the time it last changed on the site it came from.
            var updatedUtc = (record as ITimestampedModel)?.UpdatedUtc;

            await store.CreateAsync(record, cancellationToken);

            if (record is ITimestampedModel timestamped && updatedUtc is { } kept && kept != default)
            {
                timestamped.UpdatedUtc = kept;
            }

            result.Imported++;
        }
    }
}

/// <summary>
/// What an import of the ledger did.
/// </summary>
public sealed class TransactionLedgerImportResult
{
    /// <summary>
    /// Gets or sets how many records were created.
    /// </summary>
    public int Imported { get; set; }

    /// <summary>
    /// Gets or sets how many records the site already had and kept.
    /// </summary>
    public int Kept { get; set; }

    /// <summary>
    /// Gets why some records were not imported.
    /// </summary>
    public IList<string> Errors { get; } = [];
}
