using System.Linq.Expressions;
using CrestApps.OrchardCore.Checkout.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Checkout.Reports.DataSources;

/// <summary>
/// One row per payment method a customer kept for charges made without them present: its kind, whether it has expired,
/// whether it is their default, and when it was kept. No card detail is exposed: not its brand, expiry or digits, nor the
/// references the provider charges it by.
/// </summary>
public sealed class SavedPaymentMethodsReportDataSet : IndexedReportDataSet<CustomerPaymentMethod, CustomerPaymentMethodIndex>, ICheckoutReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the payment method was kept, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// The name of the field that holds the account of the customer who kept the payment method.
    /// </summary>
    public const string UserIdField = "UserId";

    /// <summary>
    /// Initializes a new instance of the <see cref="SavedPaymentMethodsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the payment methods are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="clock">The clock, to tell whether a card has expired.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SavedPaymentMethodsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IClock clock,
        IStringLocalizer<SavedPaymentMethodsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CheckoutSource.SavedPaymentMethods,
                stringLocalizer["Saved payment methods"],
                stringLocalizer["One row per payment method a customer kept for later charges: its kind, whether it has expired, whether it is their default, and when it was kept."]),
            session,
            authorizationService,
            null,
            TransactionsPermissions.ManageRefunds)
    {
        var S = stringLocalizer;
        var method = S["Payment method"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Saved payment method ID"], DataFieldType.Text, record => record.ItemId, method).Identifier();
        AddField("ProviderKey", S["Payment method"], DataFieldType.Text, record => record.PaymentMethod?.ProviderKey, method);
        AddField("IsExpired", S["Expired"], DataFieldType.Boolean, record => record.PaymentMethod?.IsExpired(clock.UtcNow) == true, method)
            .Description = S["Whether the card's expiry has passed when the report runs."].Value;
        AddField(nameof(CustomerPaymentMethod.IsDefault), S["Default"], DataFieldType.Boolean, record => record.IsDefault, method);
        AddField(UserIdField, S["User ID"], DataFieldType.Text, record => record.OwnerId, method).Identifier(ReportDataReferences.User());
        AddField(CreatedUtcField, S["Kept"], DataFieldType.DateTime, record => record.CreatedUtc, method);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CustomerPaymentMethodIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CustomerPaymentMethodIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CustomerPaymentMethodIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [UserIdField] = index => index.OwnerId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CustomerPaymentMethodIndex> columns)
    {
        columns.Text("ProviderKey", nameof(CustomerPaymentMethodIndex.ProviderKey));
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The customer's account is kept in a column of another name.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal)
        {
            [UserIdField] = nameof(CustomerPaymentMethodIndex.OwnerId),
        };

        return columns;
    }
}
