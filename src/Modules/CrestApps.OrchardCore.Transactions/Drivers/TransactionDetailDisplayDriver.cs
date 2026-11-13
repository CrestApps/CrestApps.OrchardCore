using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Builds the administration detail page of a transaction, display type <see cref="TransactionsConstants.DisplayTypes.DetailAdmin"/>:
/// the summary and the timeline in the <c>Content</c> zone and the actions in the <c>Actions</c> zone. Another module adds
/// a card to the page with its own <see cref="DisplayDriver{TModel}"/> of <see cref="Transaction"/> for the same display
/// type, such as the document a transaction was posted for.
/// </summary>
public sealed class TransactionDetailDisplayDriver : DisplayDriver<Transaction>
{
    private readonly IUserService _userService;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionDetailDisplayDriver"/> class.
    /// </summary>
    /// <param name="userService">The user service used to resolve the owner.</param>
    /// <param name="displayNameProvider">The display name provider used to name the owner.</param>
    /// <param name="serviceProvider">The service provider the optional services (reminders, receipts, reference describers) are resolved from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionDetailDisplayDriver(
        IUserService userService,
        IDisplayNameProvider displayNameProvider,
        IServiceProvider serviceProvider,
        IStringLocalizer<TransactionDetailDisplayDriver> stringLocalizer)
    {
        _userService = userService;
        _displayNameProvider = displayNameProvider;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(Transaction transaction, BuildDisplayContext context)
    {
        if (context.DisplayType != TransactionsConstants.DisplayTypes.DetailAdmin)
        {
            return null;
        }

        var ownerName = await ResolveOwnerNameAsync(transaction.OwnerId) ?? TransactionOwnerNames.GuestName(transaction, S["{0} (guest)"].Value);
        var reference = await DescribeReferenceAsync(transaction);
        var canSendReminder = _serviceProvider.GetService<ITransactionReminderService>() is not null;
        var showReceipts = _serviceProvider.GetService<ITransactionReceiptBuilder>() is not null;
        var payer = transaction.GetPayer();
        var payerName = transaction.PayerContact?.Name;

        if (string.IsNullOrEmpty(payerName) && !payer.IsEmpty && _serviceProvider.GetService<IPartyDirectory>() is { } parties)
        {
            payerName = await parties.GetNameAsync(payer);
        }

        // Customer credit is offered only when the Store Credit feature keeps it and the payer can hold it.
        var credit = _serviceProvider.GetService<ICustomerCreditService>();
        var canHoldCredit = credit is not null && credit.CanHold(payer);
        var creditBalance = canHoldCredit
            ? await credit.GetBalanceAsync(payer, transaction.Currency)
            : 0m;

        void Fill(TransactionDetailViewModel model)
        {
            model.Transaction = transaction;
            model.OwnerName = ownerName;
            model.PayerName = payerName;
            model.Reference = reference;
            model.CanManage = true;
            model.CanSendReminder = canSendReminder;
            model.ShowReceipts = showReceipts;
            model.CanHoldCredit = canHoldCredit;
            model.CreditBalance = creditBalance;
        }

        return Combine(
            Initialize<TransactionDetailViewModel>("TransactionSummary_DetailAdmin", Fill).Location(TransactionsConstants.DisplayTypes.DetailAdmin, "Content:5"),
            Initialize<TransactionDetailViewModel>("TransactionTimeline_DetailAdmin", Fill).Location(TransactionsConstants.DisplayTypes.DetailAdmin, "Content:20"),
            Initialize<TransactionDetailViewModel>("TransactionActions_DetailAdmin", Fill).Location(TransactionsConstants.DisplayTypes.DetailAdmin, "Actions:5"));
    }

    // What the transaction paid for, such as an order, named and linked by the module that owns it.
    private async Task<CheckoutReferenceDescription> DescribeReferenceAsync(Transaction transaction)
    {
        if (string.IsNullOrEmpty(transaction.ReferenceType) || string.IsNullOrEmpty(transaction.ReferenceId))
        {
            return null;
        }

        foreach (var describer in _serviceProvider.GetServices<ICheckoutReferenceDescriber>())
        {
            if (await describer.DescribeAsync(transaction.ReferenceType, transaction.ReferenceId, CancellationToken.None) is { Text.Length: > 0 } description)
            {
                return description;
            }
        }

        return null;
    }

    private async Task<string> ResolveOwnerNameAsync(string ownerId)
    {
        if (string.IsNullOrEmpty(ownerId))
        {
            return null;
        }

        var user = await _userService.GetUserByUniqueIdAsync(ownerId);

        return user is null ? null : await _displayNameProvider.GetAsync(user);
    }
}
