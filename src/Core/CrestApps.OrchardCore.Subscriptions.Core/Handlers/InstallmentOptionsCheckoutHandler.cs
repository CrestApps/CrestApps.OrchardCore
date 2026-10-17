using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Adds the "Choose how to pay" step to a checkout that pays an outstanding transaction, when installments are
/// offered for it: the customer picks paying in full or one of the installment options before they pay.
/// </summary>
/// <remarks>
/// The step collects data, so the payment step can't be reached until the customer has chosen. Choosing installments
/// turns the transaction into a plan (see <c>IInstallmentPlanService.CreateFromTransactionAsync</c>), and the same
/// checkout then takes the plan's first payment and keeps the card.
/// </remarks>
public sealed class InstallmentOptionsCheckoutHandler : CheckoutHandlerBase
{
    /// <summary>
    /// The position of the step: after the balance it splits, before the payment.
    /// </summary>
    public const int StepOrder = 10;

    private readonly ITransactionManager _transactionManager;
    private readonly InstallmentOptionResolver _optionResolver;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentOptionsCheckoutHandler"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="optionResolver">The resolver of the options offered for a transaction.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InstallmentOptionsCheckoutHandler(
        ITransactionManager transactionManager,
        InstallmentOptionResolver optionResolver,
        IStringLocalizer<InstallmentOptionsCheckoutHandler> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _optionResolver = optionResolver;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var session = context.Session;

        if (!string.Equals(session.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(session.ReferenceId) ||
            session.Steps.Any(step => string.Equals(step.Key, SubscriptionConstants.InstallmentPlans.CheckoutStepKey, StringComparison.Ordinal)))
        {
            return;
        }

        var transaction = await _transactionManager.FindByIdAsync(session.ReferenceId);

        if (transaction is null)
        {
            return;
        }

        var options = await _optionResolver.GetOptionsAsync(transaction);

        if (options.Count == 0)
        {
            // Nothing to choose between, so the checkout opens on the payment as it always did.
            return;
        }

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = SubscriptionConstants.InstallmentPlans.CheckoutStepKey,
            Title = S["Choose how to pay"],
            Description = transaction.Title,
            Order = StepOrder,
            CollectData = true,
        });
    }
}
