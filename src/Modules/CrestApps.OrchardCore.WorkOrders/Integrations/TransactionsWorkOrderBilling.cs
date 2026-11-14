using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.WorkOrders.Integrations;

/// <summary>
/// Invoices a completed work order as a Transactions invoice that refers to it.
/// </summary>
internal sealed class TransactionsWorkOrderBilling : IWorkOrderBilling
{
    /// <summary>
    /// The source of the invoices.
    /// </summary>
    public const string Source = "WorkOrders";

    private readonly ITransactionManager _transactions;
    private readonly ITransactionInvoiceService _invoices;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionsWorkOrderBilling"/> class.
    /// </summary>
    /// <param name="transactions">The transaction manager.</param>
    /// <param name="invoices">The invoice service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionsWorkOrderBilling(ITransactionManager transactions, ITransactionInvoiceService invoices, IClock clock, IStringLocalizer<TransactionsWorkOrderBilling> stringLocalizer)
    {
        _transactions = transactions;
        _invoices = invoices;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<string> InvoiceAsync(WorkOrder workOrder, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var transaction = await _transactions.NewAsync(cancellationToken: cancellationToken);

        transaction.Title = S["Work order {0}: {1}", workOrder.Number, workOrder.Summary].Value;
        transaction.Source = Source;
        // The customer user may pay it themselves; with no user, the contact is reached by e-mail as a guest owner. The
        // account, when there is one, owes it, so its admins see and pay it; otherwise the customer or the contact does.
        if (!string.IsNullOrEmpty(workOrder.CustomerId))
        {
            transaction.OwnerId = workOrder.CustomerId;
            transaction.OwnerKind = CustomerOwnerKind.Authenticated;
        }
        else
        {
            transaction.OwnerId = workOrder.ItemId;
            transaction.OwnerKind = CustomerOwnerKind.Guest;
        }

        transaction.SetPayer(GetPayer(workOrder));

        if (!string.IsNullOrWhiteSpace(workOrder.Contact?.Name) || !string.IsNullOrWhiteSpace(workOrder.Contact?.Email))
        {
            transaction.PayerContact = ContactInfo.From(workOrder.Contact.Name, workOrder.Contact.Email, workOrder.Contact.Phone);
        }

        transaction.GuestContactName = workOrder.Contact?.Name;
        transaction.GuestContactEmail = workOrder.Contact?.Email;
        transaction.ReferenceType = WorkOrdersConstants.ReferenceType;
        transaction.ReferenceId = workOrder.ItemId;
        transaction.Currency = currency;
        transaction.Amount = amount;
        transaction.TaxAmount = 0m;
        transaction.TotalAmount = amount;
        transaction.Status = TransactionStatus.Outstanding;
        transaction.CreatedUtc = now;
        transaction.UpdatedUtc = now;
        transaction.DueUtc = now.AddDays(30);
        transaction.Events.Add(new TransactionEvent { CreatedUtc = now, Type = TransactionEventType.Created, Message = S["Invoiced when work order {0} was completed.", workOrder.Number].Value });

        await _invoices.EnsureInvoiceNumberAsync(transaction, cancellationToken);
        await _transactions.CreateAsync(transaction, cancellationToken);

        return transaction.ItemId;
    }

    // Who owes the invoice: the work order's account, else its customer user, else its CRM contact.
    private static PartyRef GetPayer(WorkOrder workOrder)
    {
        if (!string.IsNullOrEmpty(workOrder.AccountId))
        {
            return new PartyRef(PartyKinds.Account, workOrder.AccountId);
        }

        if (!string.IsNullOrEmpty(workOrder.CustomerId))
        {
            return new PartyRef(PartyKinds.User, workOrder.CustomerId);
        }

        return string.IsNullOrEmpty(workOrder.ContactId)
            ? default
            : new PartyRef(PartyKinds.Contact, workOrder.ContactId);
    }
}
