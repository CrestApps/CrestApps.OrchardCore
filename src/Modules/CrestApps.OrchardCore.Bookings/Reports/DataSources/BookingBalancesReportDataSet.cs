using System.Linq.Expressions;
using CrestApps.OrchardCore.Bookings.Indexes;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Bookings.Reports.DataSources;

/// <summary>
/// One row per balance left to pay on an order whose bookings were paid by deposit: how much, when it is due, and
/// whether charging the kept card worked. Money is in the balance's currency, which the <c>Currency</c> field names.
/// The card and the payment provider's error messages are never exposed.
/// </summary>
public sealed class BookingBalancesReportDataSet : IndexedReportDataSet<BookingBalance, BookingBalanceIndex>, IBookingReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the balance is due, the data set's main date.
    /// </summary>
    public const string DueUtcField = "DueUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingBalancesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session balances are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingBalancesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<BookingBalancesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.BookingSource.Balances,
                stringLocalizer["Booking balances"],
                stringLocalizer["One row per balance left to pay on an order whose bookings were paid by deposit: the amount in its currency, when it is due, and whether it was charged."]),
            session,
            authorizationService,
            BookingsConstants.CollectionName,
            BookingsPermissions.ManageBookings)
    {
        var S = stringLocalizer;
        var balance = S["Balance"].Value;
        var buyer = S["Buyer"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Balance ID"], DataFieldType.Text, record => record.ItemId, balance).Identifier();
        AddField(nameof(BookingBalance.Status), S["Status"], DataFieldType.Text, record => record.Status, balance).Description = S["Scheduled, Paid, Failed or Canceled."].Value;
        AddField(nameof(BookingBalance.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, balance);
        AddField(nameof(BookingBalance.Amount), S["Amount"], DataFieldType.Decimal, record => record.Amount, balance).Description = S["In the balance's currency."].Value;
        AddField(nameof(BookingBalance.Attempts), S["Charge attempts"], DataFieldType.Integer, record => record.Attempts, balance);
        AddField(nameof(BookingBalance.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, balance)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(BookingBalance.OrderNumber), S["Order number"], DataFieldType.Text, record => record.OrderNumber, balance);
        AddField(nameof(BookingBalance.TransactionId), S["Transaction ID"], DataFieldType.Text, record => record.TransactionId, balance)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.TransactionSource.Name, CommerceReportDataSets.TransactionSource.Transactions));
        AddField(nameof(BookingBalance.DepositSessionId), S["Deposit checkout ID"], DataFieldType.Text, record => record.DepositSessionId, balance)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions));

        AddField("UserId", S["User ID"], DataFieldType.Text, record => record.OwnerId, buyer).Identifier(ReportDataReferences.User());

        AddField(DueUtcField, S["Due"], DataFieldType.DateTime, record => record.DueUtc, dates);
        AddField(nameof(BookingBalance.LastAttemptUtc), S["Last charge attempt"], DataFieldType.DateTime, record => record.LastAttemptUtc, dates);
        AddField(nameof(BookingBalance.NextAttemptUtc), S["Next charge attempt"], DataFieldType.DateTime, record => record.NextAttemptUtc, dates);
        AddField(nameof(BookingBalance.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<BookingBalanceIndex, DateTime>> Column)? DateColumn
        => (DueUtcField, index => index.DueUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<BookingBalanceIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<BookingBalanceIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(BookingBalance.OrderId)] = index => index.OrderId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<BookingBalanceIndex> columns)
    {
        columns.Enum(nameof(BookingBalance.Status), index => index.Status);
    }
}
