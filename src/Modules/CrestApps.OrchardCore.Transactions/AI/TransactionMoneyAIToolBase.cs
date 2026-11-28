using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// The base of the transaction tools that move money or credit (the owner's review, F23): taking back an entry, paying
/// with customer credit and adjusting what is owed. <c>previewTransactionAction</c> checks an action exactly as the tool
/// that acts will (<see cref="ReadActionAsync"/>) and returns a token (<see cref="AIActionPreviewTokens"/>, purpose
/// <see cref="PreviewPurpose"/>) bound to the tenant, the caller and the action's fingerprint, which holds what was owed
/// and the transaction's status at the preview; the tool that acts requires that token and checks the action again. A
/// payment or an entry that lands in between makes the token fail, so the user sees a new preview. Amounts are always
/// given with the transaction's currency, everything owed needs an explicit flag, and free text never carries a card or
/// bank account number (<see cref="AIMoneyArguments"/>).
/// </summary>
internal abstract class TransactionMoneyAIToolBase : TransactionAIToolBase
{
    /// <summary>
    /// The cache-key purpose of the transaction preview tokens, so they never mix with another module's.
    /// </summary>
    public const string PreviewPurpose = "transactions-money-preview";

    /// <summary>
    /// The preview tool the actions require a token from.
    /// </summary>
    public const string PreviewToolName = "previewTransactionAction";

    /// <summary>
    /// The action that takes back a payment recorded by hand or an adjustment.
    /// </summary>
    public const string ReverseAction = "reverse";

    /// <summary>
    /// The action that pays with the payer's customer credit.
    /// </summary>
    public const string ApplyCreditAction = "applyCredit";

    /// <summary>
    /// The action that lowers what is owed without money being received.
    /// </summary>
    public const string AdjustAction = "adjust";

    /// <summary>
    /// The longest note or reason, as the forms of the transaction's page take it.
    /// </summary>
    protected const int MaxTextLength = 500;

    /// <summary>
    /// Writes the fingerprint of an action: the tool family first, then its parts, with numbers written without trailing
    /// zeros so 25.5 and 25.50 are the same action.
    /// </summary>
    /// <param name="family">The tool family, such as <c>transaction-reverse</c>.</param>
    /// <param name="parts">The parts, such as the transaction, the amount and the currency.</param>
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
    /// Writes the fingerprint of what was asked, without the transaction's state, which an idempotency key is bound to:
    /// a retry of an action that went through gets its first answer back even though what is owed has changed.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The fingerprint.</returns>
    protected static string RequestFingerprint(string action, Transaction transaction, AIFunctionArguments arguments)
    {
        var amount = TryReadDecimal(arguments, "amount", out var value) ? (object)value : ReadString(arguments, "amount");
        var currency = ReadString(arguments, "currency")?.ToUpperInvariant();
        var entireBalance = ReadBool(arguments, "entireBalance") == true;

        return action == AdjustAction
            ? Fingerprint("transaction-" + action, transaction.ItemId, amount, currency, ReadString(arguments, "kind")?.ToLowerInvariant(), entireBalance, ReadString(arguments, "reason"))
            : Fingerprint("transaction-" + action, transaction.ItemId, amount, currency, entireBalance, ReadString(arguments, "note"));
    }

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
            return (null, null, "Nothing was previewed: the site can't keep preview tokens right now, so this can't be done from a chat. Do it on the transaction's page.");
        }

        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var (token, expiresUtc) = await AIActionPreviewTokens.IssueAsync(cache, PreviewPurpose, GetPreviewScope(services, caller), fingerprint, utcNow, cancellationToken);

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
    /// <param name="refused">How a refusal starts, such as "Nothing was taken back".</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when the token was taken, or the sentence that says why not.</returns>
    protected static async Task<string> TakePreviewTokenAsync(IServiceProvider services, ClaimsPrincipal caller, string token, string fingerprint, string refused, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return $"{refused}: call {PreviewToolName} first with the same arguments, show its result to the user, and pass the previewToken it returns. A token works once and expires 10 minutes after the preview.";
        }

        if (services.GetService<IDistributedCache>() is not { } cache || services.GetService<IDistributedLock>() is not { } locks)
        {
            return $"{refused}: the site can't check preview tokens right now. Do it on the transaction's page.";
        }

        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var taken = await AIActionPreviewTokens.TakeAsync(cache, locks, PreviewPurpose, GetPreviewScope(services, caller), token, fingerprint, utcNow, cancellationToken);

        return taken switch
        {
            AIActionPreviewTokenResult.Taken => null,
            AIActionPreviewTokenResult.Mismatch => $"{refused}: the previewToken was issued for other arguments, or the transaction changed since the preview. Call {PreviewToolName} again with these arguments, show the result to the user, and use the new token.",
            AIActionPreviewTokenResult.Expired => $"{refused}: the previewToken expired. Call {PreviewToolName} again, show the result to the user, and use the new token.",
            AIActionPreviewTokenResult.Busy => $"{refused}: another call is using the same previewToken at this moment. Read the transaction again in your next message.",
            _ => $"{refused}: the previewToken is not one {PreviewToolName} issued to you, or it was already used or has expired. Call {PreviewToolName} again, show the result to the user, and use the new token.",
        };
    }

    /// <summary>
    /// Reads a reason or note: optional or required, at most <see cref="MaxTextLength"/> characters, and never holding a
    /// card or bank account number.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="required">Whether it must be given.</param>
    /// <param name="refused">How a refusal starts.</param>
    /// <param name="missing">The refusal when a required text is missing.</param>
    /// <returns>The text, or the sentence that says why it is refused.</returns>
    protected static (string Text, string Error) ReadText(AIFunctionArguments arguments, string name, bool required, string refused, string missing = null)
    {
        var text = ReadString(arguments, name);

        if (text is null)
        {
            return required ? (null, $"{refused}: {missing ?? $"give the {name}."}") : (null, null);
        }

        if (text.Length > MaxTextLength)
        {
            return (null, $"{refused}: the {name} is limited to {MaxTextLength} characters.");
        }

        return AIMoneyArguments.RefuseAccountNumbers(refused, text) is { } error ? (null, error) : (text, null);
    }

    /// <summary>
    /// Reads and checks an action's arguments against the transaction as it is now: the same checks for the preview and
    /// for the tool that acts, so a token is issued only for an action the tool would accept.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="action">The action: <see cref="ReverseAction"/>, <see cref="ApplyCreditAction"/> or <see cref="AdjustAction"/>.</param>
    /// <param name="verb">What was not done, for the refusals, such as "previewed".</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The checked action, or the sentence that says why it is refused.</returns>
    protected static async Task<(TransactionActionRequest Request, string Error)> ReadActionAsync(
        IServiceProvider services,
        AIFunctionArguments arguments,
        Transaction transaction,
        string action,
        string verb,
        CancellationToken cancellationToken)
    {
        var refused = $"Nothing was {verb}";

        if (!TryReadDecimal(arguments, "amount", out var amount))
        {
            return (null, $"{refused}: amount must be a number.");
        }

        return action switch
        {
            ReverseAction => await ReadReversalAsync(services, arguments, transaction, amount, verb, refused, cancellationToken),
            ApplyCreditAction => await ReadApplyCreditAsync(services, arguments, transaction, amount, verb, refused, cancellationToken),
            AdjustAction => ReadAdjustment(arguments, transaction, amount, verb, refused),
            _ => (null, $"{refused}: the action must be {ReverseAction}, {ApplyCreditAction} or {AdjustAction}."),
        };
    }

    // Takes back a payment recorded by hand or an adjustment, whole, as DefaultTransactionCommandService.ReverseAsync does:
    // a payment taken online is refunded instead, and credit the entry moved must move back with it.
    private static async Task<(TransactionActionRequest Request, string Error)> ReadReversalAsync(
        IServiceProvider services,
        AIFunctionArguments arguments,
        Transaction transaction,
        decimal? amount,
        string verb,
        string refused,
        CancellationToken cancellationToken)
    {
        var eventId = ReadString(arguments, "eventId");

        if (eventId is null)
        {
            return (null, $"{refused}: give the eventId of the timeline entry to take back, from getTransaction's timeline.");
        }

        var entry = transaction.Events?.FirstOrDefault(candidate => string.Equals(candidate.Id, eventId, StringComparison.Ordinal));

        if (entry is null)
        {
            return (null, $"{refused}: the transaction has no timeline entry with the id '{eventId}'. Read its timeline with getTransaction.");
        }

        if (entry.Type == TransactionEventType.PaymentRecorded && !string.IsNullOrEmpty(entry.PaymentAttemptId))
        {
            return (null, $"{refused}: this payment was taken online. Give it back with refundPayment, or refundOrder for an order's payment, instead of taking it back.");
        }

        if (entry.Type is not (TransactionEventType.PaymentRecorded or TransactionEventType.Adjusted))
        {
            return (null, $"{refused}: only a payment recorded by hand or an adjustment can be taken back; this entry is {ToCamel(entry.Type.ToString())}.");
        }

        if (!TransactionStateMachine.CanReverse(transaction, entry))
        {
            return (null, $"{refused}: this entry can't be taken back: it was taken back already, or the transaction is {ToCamel(transaction.Status.ToString())}.");
        }

        if (CheckAmount(transaction, amount, ReadString(arguments, "currency"), verb) is { } amountError)
        {
            return (null, amountError);
        }

        var entryAmount = CurrencyScale.Round(entry.Amount ?? 0m, transaction.Currency);

        if (amount.Value != entryAmount)
        {
            return (null, $"{refused}: the entry is {Format(entryAmount, transaction.Currency)} and is taken back whole. Give that amount.");
        }

        // Only credit that was actually changed moves back: a payment made with credit gives it back, and a payment whose
        // excess was kept as credit takes that credit back.
        var creditBack = entry.CreditState == TransactionCreditState.Settled ? -(entry.CreditChange ?? 0m) : 0m;
        decimal? balance = null;

        if (creditBack != 0m)
        {
            var credit = services.GetService<ICustomerCreditService>();
            var payer = transaction.GetPayer();

            if (credit is null || payer.IsEmpty || !credit.CanHold(payer))
            {
                return (null, $"{refused}: this entry moved customer credit, which must move back with it, and the payer's customer credit isn't available here (it needs the Customers Store Credit feature). Take it back on the transaction's page.");
            }

            balance = CurrencyScale.Round(await credit.GetBalanceAsync(payer, transaction.Currency, cancellationToken), transaction.Currency);

            if (creditBack < 0m && balance < -creditBack)
            {
                return (null, $"{refused}: the {Format(-creditBack, transaction.Currency)} this payment added to the customer's credit must come back with it, and the customer has only {Format(balance.Value, transaction.Currency)} of credit.");
            }
        }

        var outstanding = Outstanding(transaction);

        return (new TransactionActionRequest(
            transaction,
            ReverseAction,
            entryAmount,
            Fingerprint("transaction-" + ReverseAction, transaction.ItemId, entry.Id, entry.Type, entryAmount, transaction.Currency, outstanding, transaction.Status, creditBack))
        {
            Entry = entry,
            CreditChange = creditBack,
            CreditBalance = balance,
            Outstanding = outstanding,
        }, null);
    }

    // Pays with the payer's customer credit, as DefaultTransactionCommandService.ApplyCreditAsync does, but refuses what the
    // command would quietly cap: an amount above what is owed or above the credit.
    private static async Task<(TransactionActionRequest Request, string Error)> ReadApplyCreditAsync(
        IServiceProvider services,
        AIFunctionArguments arguments,
        Transaction transaction,
        decimal? amount,
        string verb,
        string refused,
        CancellationToken cancellationToken)
    {
        if (services.GetService<ICustomerCreditService>() is not { } credit)
        {
            return (null, $"{refused}: paying with customer credit needs the Customers Store Credit feature.");
        }

        if (!TransactionStateMachine.CanRecordPayment(transaction))
        {
            return (null, $"{refused}: the transaction is {ToCamel(transaction.Status.ToString())} with {Format(Outstanding(transaction), transaction.Currency)} owed, so it takes no payment.");
        }

        var payer = transaction.GetPayer();

        if (payer.IsEmpty || !credit.CanHold(payer))
        {
            return (null, $"{refused}: the transaction's payer can't hold customer credit (a guest, or no payer).");
        }

        if (CheckAmount(transaction, amount, ReadString(arguments, "currency"), verb) is { } amountError)
        {
            return (null, amountError);
        }

        var outstanding = Outstanding(transaction);
        var entireBalance = ReadBool(arguments, "entireBalance") == true;

        if (CheckWithinOwed(amount.Value, outstanding, entireBalance, transaction.Currency, refused, "pay it all with credit") is { } owedError)
        {
            return (null, owedError);
        }

        var balance = CurrencyScale.Round(await credit.GetBalanceAsync(payer, transaction.Currency, cancellationToken), transaction.Currency);

        if (amount.Value > balance)
        {
            return (null, $"{refused}: the customer has only {Format(balance, transaction.Currency)} of credit.");
        }

        var (note, noteError) = ReadText(arguments, "note", required: false, refused);

        if (noteError is not null)
        {
            return (null, noteError);
        }

        return (new TransactionActionRequest(
            transaction,
            ApplyCreditAction,
            amount.Value,
            Fingerprint("transaction-" + ApplyCreditAction, transaction.ItemId, amount.Value, transaction.Currency, entireBalance, note, outstanding, transaction.Status, balance))
        {
            EntireBalance = entireBalance,
            Note = note,
            CreditChange = -amount.Value,
            CreditBalance = balance,
            Outstanding = outstanding,
        }, null);
    }

    // Lowers what is owed, as DefaultTransactionCommandService.AdjustAsync does: never more than what is still owed.
    private static (TransactionActionRequest Request, string Error) ReadAdjustment(
        AIFunctionArguments arguments,
        Transaction transaction,
        decimal? amount,
        string verb,
        string refused)
    {
        if (!Enum.TryParse<TransactionAdjustmentKind>(ReadString(arguments, "kind"), ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return (null, $"{refused}: kind must be discount, waiver, credit or writeOff.");
        }

        if (!TransactionStateMachine.CanAdjust(transaction))
        {
            return (null, $"{refused}: the transaction is {ToCamel(transaction.Status.ToString())} with {Format(Outstanding(transaction), transaction.Currency)} owed, so nothing can be taken off.");
        }

        if (CheckAmount(transaction, amount, ReadString(arguments, "currency"), verb) is { } amountError)
        {
            return (null, amountError);
        }

        var outstanding = Outstanding(transaction);
        var entireBalance = ReadBool(arguments, "entireBalance") == true;

        if (CheckWithinOwed(amount.Value, outstanding, entireBalance, transaction.Currency, refused, "take it all off") is { } owedError)
        {
            return (null, owedError);
        }

        return (new TransactionActionRequest(
            transaction,
            AdjustAction,
            amount.Value,
            Fingerprint("transaction-" + AdjustAction, transaction.ItemId, amount.Value, transaction.Currency, kind, entireBalance, outstanding, transaction.Status))
        {
            EntireBalance = entireBalance,
            Kind = kind,
            Outstanding = outstanding,
        }, null);
    }

    // An amount is never more than what is still owed, and it is everything owed exactly when entireBalance says so.
    private static string CheckWithinOwed(decimal amount, decimal outstanding, bool entireBalance, string currency, string refused, string allOfIt)
    {
        if (amount > outstanding)
        {
            return $"{refused}: only {Format(outstanding, currency)} is still owed on this transaction.";
        }

        if (entireBalance && amount != outstanding)
        {
            return $"{refused}: everything still owed is {Format(outstanding, currency)}. Give that amount with entireBalance, or a smaller amount without it.";
        }

        return !entireBalance && amount == outstanding
            ? $"{refused}: {Format(amount, currency)} is everything still owed. Set entireBalance to true to {allOfIt}."
            : null;
    }

    private static string GetPreviewScope(IServiceProvider services, ClaimsPrincipal caller)
        => $"{services.GetService<ShellSettings>()?.Name}|ai-tool:transactions-money|{caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.Identity?.Name}";
}

/// <summary>
/// A transaction action whose arguments passed the checks.
/// </summary>
/// <param name="Transaction">The transaction, as it was read.</param>
/// <param name="Action">The action.</param>
/// <param name="Amount">The amount, in the transaction's currency.</param>
/// <param name="Fingerprint">The action's fingerprint, with the transaction's state, which a preview's token is bound to.</param>
internal sealed record TransactionActionRequest(Transaction Transaction, string Action, decimal Amount, string Fingerprint)
{
    /// <summary>
    /// Gets the timeline entry taken back.
    /// </summary>
    public TransactionEvent Entry { get; init; }

    /// <summary>
    /// Gets how the payer's customer credit changes: more than zero when credit is given back, less when it is spent or
    /// taken back.
    /// </summary>
    public decimal CreditChange { get; init; }

    /// <summary>
    /// Gets the payer's customer credit in the transaction's currency, when credit moves.
    /// </summary>
    public decimal? CreditBalance { get; init; }

    /// <summary>
    /// Gets a value indicating whether the amount is everything still owed.
    /// </summary>
    public bool EntireBalance { get; init; }

    /// <summary>
    /// Gets the note kept with a payment made with credit.
    /// </summary>
    public string Note { get; init; }

    /// <summary>
    /// Gets why an adjustment takes the amount off.
    /// </summary>
    public TransactionAdjustmentKind? Kind { get; init; }

    /// <summary>
    /// Gets what was still owed when the action was checked.
    /// </summary>
    public decimal Outstanding { get; init; }
}
