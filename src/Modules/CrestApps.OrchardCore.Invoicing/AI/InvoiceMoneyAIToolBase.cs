using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// The base of the invoicing tools that move money or credit (the owner's review, F23): voiding, writing off and
/// recording payments here, and issuing credit notes. A preview tool checks the action exactly as the tool that acts will
/// and returns a token (<see cref="AIActionPreviewTokens"/>, purpose <see cref="PreviewPurpose"/>) bound to the tenant,
/// the caller and the action's fingerprint; the tool that acts requires that token, checks the action again, and, when
/// the domain command is not idempotent itself, runs once per idempotency key (<see cref="IdempotentOperation"/>).
/// Amounts are always given with their currency, capped by what the ledger allows, and everything needs an explicit
/// flag. Free text never carries a card or bank account number.
/// </summary>
/// <remarks>
/// The first part of every fingerprint names the tool family (such as <c>invoice-void</c> or <c>credit-note-issue</c>),
/// so a token previewed for one action never works for another.
/// </remarks>
internal abstract class InvoiceMoneyAIToolBase : InvoiceAIToolBase
{
    /// <summary>
    /// The cache-key purpose of the invoicing preview tokens, so they never mix with another module's.
    /// </summary>
    public const string PreviewPurpose = "invoicing-money-preview";

    private static readonly TimeSpan _previewDraftLifetime = TimeSpan.FromDays(1);

    /// <summary>
    /// The longest idempotency key.
    /// </summary>
    protected const int MaxKeyLength = 128;

    private static readonly TimeSpan _keyLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Checks an amount against a limit with the money rules: it is given and more than zero, in the document's currency
    /// and within its decimal places, no more than the limit, and equal to the limit exactly when the flag that asks for
    /// everything is set.
    /// </summary>
    /// <param name="amount">The amount given.</param>
    /// <param name="currency">The currency given.</param>
    /// <param name="documentCurrency">The document's currency.</param>
    /// <param name="limit">The most the action can take, such as what the ledger still has owed.</param>
    /// <param name="all">Whether the flag that asks for everything is set.</param>
    /// <param name="refused">How a refusal starts, such as "Nothing was recorded".</param>
    /// <param name="limitDescription">What the limit is, for the sentences, such as "still owed on invoice INV-1001".</param>
    /// <param name="allFlagName">The name of the flag that asks for everything, such as <c>entireBalance</c>.</param>
    /// <returns>The sentence that says why the amount is refused, or <see langword="null"/>.</returns>
    protected static string CheckAmount(decimal? amount, string currency, string documentCurrency, decimal limit, bool all, string refused, string limitDescription, string allFlagName = "entireBalance")
    {
        if (amount is not > 0m)
        {
            return $"{refused}: give the amount, more than zero.";
        }

        if (!string.Equals(currency?.Trim(), documentCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return $"{refused}: the document is in {documentCurrency}; give the amount in {documentCurrency}.";
        }

        if (CurrencyScale.Round(amount.Value, documentCurrency) != amount.Value)
        {
            return $"{refused}: {amount.Value.ToString(CultureInfo.InvariantCulture)} has more decimal places than {documentCurrency} allows.";
        }

        if (limit <= 0m)
        {
            return $"{refused}: nothing is {limitDescription}.";
        }

        if (amount.Value > limit)
        {
            return $"{refused}: only {Format(limit, documentCurrency)} is {limitDescription}.";
        }

        if (all && amount.Value != limit)
        {
            return $"{refused}: everything {limitDescription} is {Format(limit, documentCurrency)}. Give that amount with {allFlagName}, or a smaller amount without it.";
        }

        if (!all && amount.Value == limit)
        {
            return $"{refused}: {Format(amount.Value, documentCurrency)} is everything {limitDescription}. Set {allFlagName} to true to take it all.";
        }

        return null;
    }

    /// <summary>
    /// Refuses free text that holds what looks like a card or bank account number (<see cref="AIMoneyArguments"/>: a run
    /// of 13 to 19 digits that passes the Luhn check, or an IBAN in capitals that passes its mod-97 check), so ordinary
    /// references such as dates and fiscal years are accepted. Such numbers are never given to the assistant or kept by it.
    /// </summary>
    /// <param name="refused">How a refusal starts, such as "Nothing was recorded".</param>
    /// <param name="texts">The texts, such as the reference, the note and the reason; blanks are ignored.</param>
    /// <returns>The sentence that says why the text is refused, or <see langword="null"/>.</returns>
    protected static string RefuseAccountNumbers(string refused, params string[] texts)
        => AIMoneyArguments.RefuseAccountNumbers(refused, texts);

    /// <summary>
    /// Reads what an issued invoice still has owed on the ledger: it brings the invoice in step with its transaction, as
    /// its page does (<see cref="InvoiceLedgerSync"/>, which only queues the update), and reads the transaction. The ledger,
    /// not the amounts mirrored on the invoice, is what the invoice commands decide from.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="invoice">The invoice.</param>
    /// <param name="refused">How a refusal starts.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invoice as it is now, its transaction and what is still owed, or the sentence that says why not.</returns>
    protected static async Task<InvoiceLedgerBalance> ReadLedgerAsync(IServiceProvider services, Invoice invoice, string refused, CancellationToken cancellationToken)
    {
        if (services.GetService<InvoiceLedgerAvailability>() is { } availability && await availability.IsMissingAsync(invoice, cancellationToken))
        {
            return new InvoiceLedgerBalance(invoice, null, 0m, $"{refused}: the ledger entry invoice {invoice.Number} posted is not on this site, so it can't be changed here. Import it with the Transactions step of a deployment plan.");
        }

        if (services.GetService<InvoiceLedgerSync>() is { } sync)
        {
            invoice = await sync.SyncAsync(invoice, cancellationToken) ?? invoice;
        }

        if (string.IsNullOrEmpty(invoice.TransactionId))
        {
            return new InvoiceLedgerBalance(invoice, null, 0m, null);
        }

        var transaction = await services.GetRequiredService<ITransactionManager>().FindByIdAsync(invoice.TransactionId, cancellationToken);

        if (transaction is null)
        {
            return new InvoiceLedgerBalance(invoice, null, 0m, $"{refused}: the ledger entry of invoice {invoice.Number} could not be found.");
        }

        var outstanding = Math.Max(0m, CurrencyScale.Round(transaction.OutstandingAmount, invoice.Currency));

        return new InvoiceLedgerBalance(invoice, transaction, outstanding, null);
    }

    /// <summary>
    /// Writes the fingerprint of an action: the tool family first, then its parts, with numbers written without trailing
    /// zeros so 25.5 and 25.50 are the same action.
    /// </summary>
    /// <param name="family">The tool family, such as <c>invoice-void</c>.</param>
    /// <param name="parts">The parts, such as the document, the amount and the currency.</param>
    /// <returns>The fingerprint.</returns>
    protected static string Fingerprint(string family, params object[] parts)
        => string.Join('|', parts.Select(part => part switch
        {
            null => string.Empty,
            decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => part.ToString(),
        }).Prepend(family));

    /// <summary>
    /// Issues a preview's token for an action, bound to the tenant, the caller and the action's fingerprint.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="fingerprint">The action's fingerprint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The token and when it expires in local time, or the sentence that says why none can be issued.</returns>
    protected static async Task<(string Token, string ExpiresAt, string Error)> IssuePreviewTokenAsync(IServiceProvider services, ClaimsPrincipal caller, string fingerprint, CancellationToken cancellationToken)
    {
        if (services.GetService<IDistributedCache>() is not { } cache)
        {
            return (null, null, "Nothing was previewed: the site can't keep preview tokens right now, so this can't be done from a chat. Do it on the invoice's page.");
        }

        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var (token, expiresUtc) = await AIActionPreviewTokens.IssueAsync(cache, PreviewPurpose, GetScope(services, caller), fingerprint, utcNow, cancellationToken);

        return (token, await LocalTimeAsync(services, expiresUtc), null);
    }

    /// <summary>
    /// Takes a preview's token for an action: it must have been issued to the caller for the same fingerprint, and not
    /// have expired or been used.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="token">The token given.</param>
    /// <param name="fingerprint">The action's fingerprint, written as the preview wrote it.</param>
    /// <param name="refused">How a refusal starts, such as "Nothing was voided".</param>
    /// <param name="previewToolName">The preview tool to call again, such as <c>previewInvoiceAction</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when the token was taken, or the sentence that says why not.</returns>
    protected static async Task<string> TakePreviewTokenAsync(IServiceProvider services, ClaimsPrincipal caller, string token, string fingerprint, string refused, string previewToolName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return $"{refused}: call {previewToolName} first with the same arguments, show its result to the user, and pass the previewToken it returns. A token works once and expires 10 minutes after the preview.";
        }

        if (services.GetService<IDistributedCache>() is not { } cache || services.GetService<IDistributedLock>() is not { } locks)
        {
            return $"{refused}: the site can't check preview tokens right now. Do it on the page.";
        }

        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var taken = await AIActionPreviewTokens.TakeAsync(cache, locks, PreviewPurpose, GetScope(services, caller), token, fingerprint, utcNow, cancellationToken);

        return taken switch
        {
            AIActionPreviewTokenResult.Taken => null,
            AIActionPreviewTokenResult.Mismatch => $"{refused}: the previewToken was issued for other arguments, or the document changed since the preview. Call {previewToolName} again with these arguments, show the result to the user, and use the new token.",
            AIActionPreviewTokenResult.Expired => $"{refused}: the previewToken expired. Call {previewToolName} again, show the result to the user, and use the new token.",
            AIActionPreviewTokenResult.Busy => $"{refused}: another call is using the same previewToken at this moment. Read the document again in your next message.",
            _ => $"{refused}: the previewToken is not one {previewToolName} issued to you, or it was already used or has expired. Call {previewToolName} again, show the result to the user, and use the new token.",
        };
    }

    /// <summary>
    /// Runs a money operation once per idempotency key, kept a day per site, tool and caller: a repeated key returns the
    /// first answer, the same key with other arguments is refused, and a key in use by a call still running is reported.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="key">The idempotency key, already checked.</param>
    /// <param name="fingerprint">The request's fingerprint.</param>
    /// <param name="noun">What the operation does, for the answers, such as "recorded".</param>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The answer.</returns>
    protected async Task<string> RunOnceAsync(
        IServiceProvider services,
        ClaimsPrincipal caller,
        string key,
        string fingerprint,
        string noun,
        Func<CancellationToken, Task<IdempotentOperationAnswer>> operation,
        CancellationToken cancellationToken)
    {
        if (services.GetService<IDistributedCache>() is not { } cache || services.GetService<IDistributedLock>() is not { } locks)
        {
            return $"Nothing was {noun}: the site can't keep idempotency keys right now, so this can't be guarded against running twice from a chat. Do it on the page.";
        }

        var scope = $"{services.GetService<ShellSettings>()?.Name}|ai-tool:{Name}|{CallerId(caller)}";
        var outcome = await IdempotentOperation.RunAsync(cache, locks, scope, key, fingerprint, _keyLifetime, operation, cancellationToken);

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was {noun} again: the idempotency key '{key}' was already used for this. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was {noun}: the idempotency key '{key}' was already used for something else. Use a new key.",
            _ => $"Nothing was {noun}: a call with the idempotency key '{key}' is still running. Read the document again in your next message.",
        };
    }

    /// <summary>
    /// Reads the idempotency key argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The key, or <see langword="null"/> when it is missing or longer than <see cref="MaxKeyLength"/>.</returns>
    protected static string ReadKey(AIFunctionArguments arguments)
        => Read(arguments, "idempotencyKey") is { Length: <= MaxKeyLength } key ? key : null;

    /// <summary>
    /// Remembers that a preview started a draft for the caller, so that previewing again, in a later message, replaces
    /// the caller's own draft instead of refusing it as someone else's work. Kept for a day.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="draftId">The id of the draft the preview started.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected static async Task RememberPreviewDraftAsync(IServiceProvider services, ClaimsPrincipal caller, string draftId, CancellationToken cancellationToken)
    {
        if (services.GetService<IDistributedCache>() is not { } cache)
        {
            return;
        }

        await cache.SetStringAsync(PreviewDraftKey(services, caller, draftId), draftId, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _previewDraftLifetime,
        }, cancellationToken);
    }

    /// <summary>
    /// Returns whether a preview of the caller started the draft, as <see cref="RememberPreviewDraftAsync"/> recorded.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="draftId">The id of the draft.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the caller's own preview started it.</returns>
    protected static async Task<bool> IsPreviewDraftAsync(IServiceProvider services, ClaimsPrincipal caller, string draftId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(draftId) || services.GetService<IDistributedCache>() is not { } cache)
        {
            return false;
        }

        return string.Equals(await cache.GetStringAsync(PreviewDraftKey(services, caller, draftId), cancellationToken), draftId, StringComparison.Ordinal);
    }

    private static string PreviewDraftKey(IServiceProvider services, ClaimsPrincipal caller, string draftId)
        => $"{PreviewPurpose}:draft|{GetScope(services, caller)}|{draftId}";

    private static string GetScope(IServiceProvider services, ClaimsPrincipal caller)
        => $"{services.GetService<ShellSettings>()?.Name}|ai-tool:invoicing-money|{CallerId(caller)}";

    private static string CallerId(ClaimsPrincipal caller)
        => caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.Identity?.Name;
}

/// <summary>
/// What an issued invoice still has owed on the ledger.
/// </summary>
/// <param name="Invoice">The invoice, brought in step with its transaction.</param>
/// <param name="Transaction">The invoice's transaction, or <see langword="null"/> when it posted none.</param>
/// <param name="Outstanding">What the transaction still has owed, at the currency's precision.</param>
/// <param name="Error">Why the ledger can't be used, or <see langword="null"/>.</param>
internal sealed record InvoiceLedgerBalance(Invoice Invoice, Transaction Transaction, decimal Outstanding, string Error);
