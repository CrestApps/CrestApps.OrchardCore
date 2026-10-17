using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Builds the row of a payment attempt in the administration payments ledger, for both admin list layouts.
/// </summary>
public sealed class PaymentAttemptDisplayDriver : DisplayDriver<PaymentAttempt>
{
    private readonly TransactionDisplayText _displayText;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentAttemptDisplayDriver"/> class.
    /// </summary>
    /// <param name="displayText">The display text service, which resolves the attempt's payment provider.</param>
    public PaymentAttemptDisplayDriver(TransactionDisplayText displayText)
    {
        _displayText = displayText;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(PaymentAttempt attempt, BuildDisplayContext context)
    {
        // A payment attempt is only displayed as a row of the ledger.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = attempt.ProviderReference?.ToLowerInvariant() ?? string.Empty;
        }

        var results = new List<IDisplayResult>
        {
            View("PaymentAttempt_Fields_SummaryAdmin", attempt)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:1"),
            View("PaymentAttempt_Description_SummaryAdmin", attempt)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("PaymentAttempt_State_SummaryAdmin", attempt)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:5"),
            View("PaymentAttempt_Method_SummaryAdmin", attempt)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Method:5"),
            View("PaymentAttempt_Updated_SummaryAdmin", attempt)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Updated:5"),
            View("PaymentAttempt_Details_SummaryAdmin", attempt)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:15")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
        };

        if (PaysTransaction(attempt))
        {
            results.Add(View("PaymentAttempt_Transaction_SummaryAdmin", attempt).Location("Actions:5"));
        }

        if (PaymentAttemptRefundPolicy.CanRefund(attempt, _displayText))
        {
            results.Add(View("PaymentAttempt_Refund_SummaryAdmin", attempt).Location("Actions:10"));
        }

        return CombineAsync(results);
    }

    private static bool PaysTransaction(PaymentAttempt attempt)
        => string.Equals(attempt.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(attempt.ReferenceId);
}
