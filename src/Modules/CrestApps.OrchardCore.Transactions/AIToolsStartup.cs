using CrestApps.Core.AI;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Transactions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Registers the transaction AI tools when the Transactions and AI features are on. Every tool needs
/// <c>ManageTransactions</c>, as Commerce › Transactions. Recording a payment, adjusting and canceling write permanent
/// entries, and sending an invoice or a reminder leaves the site, so they ask for approval; recording a payment and
/// adjusting need an amount, the transaction's currency and an idempotency key, and adjusting also needs the token of
/// <c>previewTransactionAction</c> (registered by <see cref="TransactionActionsAIToolsStartup"/>).
/// </summary>
[Feature(TransactionsConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListTransactionsTool>(ListTransactionsTool.TheName)
            .WithTitle(S["List Transactions"])
            .WithDescription(S["Lists transactions (what customers owe or paid) with title, source, status, total, paid, outstanding, due date and invoice number. Filters by text, status (outstanding, pending, partially paid, paid, canceled, failed, abandoned, refunded) and source, with paging."])
            .WithCategory(S["Payments"])
            .Selectable();

        services.AddCoreAITool<GetTransactionTool>(GetTransactionTool.TheName)
            .WithTitle(S["Get Transaction"])
            .WithDescription(S["Returns one transaction with its amounts, tax lines, exchange rate, owner or guest contact, the payer and their customer credit, what it paid for (such as an order), whether part payments are accepted online, due date, reminders sent or paused, a scheduled card charge, its timeline of events (with receipt numbers, how and when a payment recorded by hand was received, the document behind an entry and the entries taken back), and what can be done with it now. Use it before recording a payment, adjusting, taking an entry back, paying with credit, canceling or chasing it."])
            .WithCategory(S["Payments"])
            .Selectable();

        services.AddCoreAITool<RecordTransactionPaymentTool>(RecordTransactionPaymentTool.TheName)
            .WithTitle(S["Record Transaction Payment"])
            .WithDescription(S["Records money received outside the site against a transaction: an amount in its currency with how it was paid, when it was received, a reference and a note, or everything still owed with entireBalance. More than is owed is refused unless creditExcess keeps the rest as the customer's credit. No money moves, but the entry cannot be removed afterwards and it can send the customer a receipt. Every call needs a new idempotency key."])
            .WithCategory(S["Payments"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<AdjustTransactionTool>(AdjustTransactionTool.TheName)
            .WithTitle(S["Adjust Transaction"])
            .WithDescription(S["Lowers what is owed on a transaction without money being received, as a discount, waiver, credit or write-off, with a reason; everything still owed needs entireBalance. Needs the previewToken from previewTransactionAction and a new idempotency key."])
            .WithCategory(S["Payments"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<CancelTransactionTool>(CancelTransactionTool.TheName)
            .WithTitle(S["Cancel Transaction"])
            .WithDescription(S["Cancels an outstanding transaction so it is no longer collected or reminded, with an optional reason. It cannot be reopened and it refunds nothing."])
            .WithCategory(S["Payments"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<SendTransactionNoticeTool>(SendTransactionNoticeTool.TheName)
            .WithTitle(S["Send Transaction Notice"])
            .WithDescription(S["Sends the transaction's owner either its invoice with the link that pays it, or a payment reminder. A reminder needs the Transaction Reminders feature and an outstanding balance."])
            .WithCategory(S["Payments"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<AddTransactionNoteTool>(AddTransactionNoteTool.TheName)
            .WithTitle(S["Add Transaction Note"])
            .WithDescription(S["Adds an internal note to a transaction's timeline under the current user's name."])
            .WithCategory(S["Payments"])
            .Selectable();
    }
}

/// <summary>
/// Registers the payment ledger AI tools (Payments, Refunds, Payment issues) when the Transactions, Checkout and AI
/// features are on, as the ledger pages need Checkout. Every tool needs <c>ManageRefunds</c>. Refunding a payment and
/// settling a refund by hand cannot be undone, so they ask for approval; a refund needs an amount, the payment's
/// currency and an idempotency key, and a payment of an order is refunded only with <c>refundOrder</c>.
/// </summary>
[Feature(TransactionsConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area, CheckoutConstants.Features.Area)]
public sealed class PaymentLedgerAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentLedgerAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentLedgerAIToolsStartup(IStringLocalizer<PaymentLedgerAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListPaymentsTool>(ListPaymentsTool.TheName)
            .WithTitle(S["List Payments"])
            .WithDescription(S["Lists payment attempts with date, payment method, state, amount, the checkout and what they paid for, and for settled ones what was already refunded and what can still be refunded. Filters by state, payment method and checkout, with paging."])
            .WithCategory(S["Payments"])
            .Selectable();

        services.AddCoreAITool<RefundPaymentTool>(RefundPaymentTool.TheName)
            .WithTitle(S["Refund Payment"])
            .WithDescription(S["Refunds an amount of one settled payment that is not an order's (such as a subscription or booking payment) through its payment provider, with a reason; everything still refundable needs refundAll. Payments of orders are refused: use refundOrder. The amount cannot exceed what is still refundable, the refund cannot be undone, and a method that cannot refund automatically is recorded for settling by hand. Every call needs a new idempotency key."])
            .WithCategory(S["Payments"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<ListRefundsTool>(ListRefundsTool.TheName)
            .WithTitle(S["List Refunds"])
            .WithDescription(S["Lists refunds with date, payment method, status (including those waiting to be settled by hand), amount, reason and provider reference. Filters by status, payment method and checkout, with paging."])
            .WithCategory(S["Payments"])
            .Selectable();

        services.AddCoreAITool<ResolveRefundTool>(ResolveRefundTool.TheName)
            .WithTitle(S["Resolve Refund"])
            .WithDescription(S["Marks a refund that is waiting for manual review as settled, with the reference of the money returned outside the site. Use it only after the customer was actually paid back; it cannot be undone and it completes the refund on the order or transaction."])
            .WithCategory(S["Payments"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<ListPaymentIssuesTool>(ListPaymentIssuesTool.TheName)
            .WithTitle(S["List Payment Issues"])
            .WithDescription(S["Lists money operations that failed and need a person, with kind, message, error, what they relate to, provider reference and amount; open ones by default."])
            .WithCategory(S["Payments"])
            .Selectable();

        services.AddCoreAITool<ResolvePaymentIssueTool>(ResolvePaymentIssueTool.TheName)
            .WithTitle(S["Resolve Payment Issue"])
            .WithDescription(S["Marks a payment issue as resolved with a note saying what was done, such as the reference of a manual refund. It only closes the issue; it performs no payment operation."])
            .WithCategory(S["Payments"])
            .Selectable();
    }
}
