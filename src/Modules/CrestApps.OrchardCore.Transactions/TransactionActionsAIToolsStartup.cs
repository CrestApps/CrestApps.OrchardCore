using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Registers the transaction actions main added to the transaction's page when the Transactions and AI features are on,
/// all under <c>ManageTransactions</c>: <c>previewTransactionAction</c>, <c>reverseTransactionEntry</c> (it moves money
/// owed and credit back and can't be undone, so it needs the preview's token and approval),
/// <c>cancelTransactionAutoCharge</c> (no admin page can schedule the charge again, so it asks for approval),
/// <c>setPartyCollectionSettings</c> (a switch that can be turned back, so no approval), and the parties' collection
/// settings as the read-only <c>partyCollectionSettings</c> kind of the generic record tools.
/// </summary>
[Feature(TransactionsConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class TransactionActionsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionActionsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionActionsAIToolsStartup(IStringLocalizer<TransactionActionsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<PreviewTransactionActionTool>(PreviewTransactionActionTool.TheName)
            .WithTitle(S["Preview Transaction Action"])
            .WithDescription(S["Shows what taking back an entry, paying with customer credit or adjusting a transaction would do, without changing anything, and returns the token the action needs."])
            .WithCategory(S["Payments"])
            .Selectable();

        services.AddCoreAITool<ReverseTransactionEntryTool>(ReverseTransactionEntryTool.TheName)
            .WithTitle(S["Reverse Transaction Entry"])
            .WithDescription(S["Takes back a payment recorded by hand, or an adjustment, so it is owed again; needs the previewToken from previewTransactionAction."])
            .WithCategory(S["Payments"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<CancelTransactionAutoChargeTool>(CancelTransactionAutoChargeTool.TheName)
            .WithTitle(S["Cancel Transaction Auto Charge"])
            .WithDescription(S["Cancels the saved-card charge scheduled on a transaction, so the owner pays the balance themselves."])
            .WithCategory(S["Payments"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<SetPartyCollectionSettingsTool>(SetPartyCollectionSettingsTool.TheName)
            .WithTitle(S["Set Party Collection Settings"])
            .WithDescription(S["Sets whether a business account or customer gets the scheduled payment reminders and is charged late fees."])
            .WithCategory(S["Payments"])
            .Selectable();

        // Read-only: the collection card on an account's or a customer's page posts to PartyCollectionSettingsController,
        // not through a display driver's editor.
        services.AddAIRecordKind<PartyCollectionSettings>(TransactionRecordKinds.PartyCollectionSettingsKind, kind => kind
            .WithTitle(S["Party collection settings"])
            .WithDescription(S["whether a party (Kind:Id, such as Account:4x7k2) is kept out of the scheduled payment reminders (remindersPaused; reminders sent by hand still go) or the late fees (lateFeesExempt) for every debt it owes or pays; only parties with one of them on are listed; the listRecords text matches a party's id or kind; use findParty for the party's name; read-only here, changed with setPartyCollectionSettings"])
            .WithPermission(TransactionsPermissions.ManageTransactions)
            .FindUsing(TransactionRecordKinds.FindPartyCollectionSettingsAsync)
            .ListUsing(TransactionRecordKinds.ListPartyCollectionSettingsAsync)
            .WithProjection(TransactionRecordKinds.ProjectPartyCollectionSettings)
            .ReadOnly(saveWith: SetPartyCollectionSettingsTool.TheName));
    }
}

/// <summary>
/// Registers <c>applyTransactionCredit</c> when the Transactions, Customers Store Credit and AI features are on, under
/// <c>ManageTransactions</c>: it spends the customer's credit and may e-mail a receipt, so it needs the preview's token, an
/// idempotency key and approval. Without Store Credit nobody can hold credit, so the tool is not offered.
/// </summary>
[Feature(TransactionsConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area, CustomersConstants.Features.StoreCredit)]
public sealed class TransactionCreditAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCreditAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionCreditAIToolsStartup(IStringLocalizer<TransactionCreditAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ApplyTransactionCreditTool>(ApplyTransactionCreditTool.TheName)
            .WithTitle(S["Apply Transaction Credit"])
            .WithDescription(S["Pays toward a transaction with its payer's customer credit; needs the previewToken from previewTransactionAction and a new idempotencyKey."])
            .WithCategory(S["Payments"])
            .AsExternal()
            .Selectable();
    }
}

/// <summary>
/// Registers the store's payment terms as the <c>paymentTerm</c> kind of the generic record tools when the Payment Terms
/// and AI features are on, under <c>ManagePaymentTerms</c>. The Payment terms page builds its editor through
/// <c>PaymentTermDisplayDriver</c>, so the kind is written by replaying that editor; a term is deleted whatever uses it,
/// as on the page, because issued documents keep its name and due date.
/// </summary>
[Feature(TransactionsConstants.Features.PaymentTerms)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class PaymentTermsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentTermsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentTermsAIToolsStartup(IStringLocalizer<PaymentTermsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<PaymentTerm>(TransactionRecordKinds.PaymentTermKind, kind => kind
            .WithTitle(S["Payment term"])
            .WithDescription(S["a payment term that sets a document's due date from its issue date (Kind: DueOnReceipt; NetDays, Days after issue; EndOfMonth, Days after the issue month ends; DayOfNextMonth, on DayOfMonth 1-31 of the next month) with an optional early-payment discount (DiscountPercent within DiscountDays); invoices, account terms, pay-later purchases and work orders use it; a term turned off (Enabled false) stays on the documents that use it, and deleting it keeps its name and due date on documents already issued"])
            .WithPermission(TransactionsPermissions.ManagePaymentTerms)
            .UseCatalogManager<IPaymentTermManager>()
            .WithProjection(TransactionRecordKinds.ProjectPaymentTerm)
            .UseEditorReplay());
    }
}
