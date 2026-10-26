using System.Linq.Expressions;
using CrestApps.OrchardCore.Bookings.Indexes;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Bookings.Reports.DataSources;

/// <summary>
/// One row per booking: what was booked, for when, for how many, where it stands, the buyer, and what it cost. Money is
/// in the booking's currency, which the <c>Currency</c> field names. The travelers' details and the reason a booking
/// was canceled are never exposed.
/// </summary>
public sealed class BookingsReportDataSet : IndexedReportDataSet<Booking, BookingIndex>, IBookingReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the booking was made, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// The name of the field that holds the booked product.
    /// </summary>
    public const string ProductIdField = "ProductId";

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session bookings are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<BookingsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.BookingSource.Bookings,
                stringLocalizer["Bookings"],
                stringLocalizer["One row per booking: the product, dates, people, status, buyer, and price in the booking's currency."]),
            session,
            authorizationService,
            BookingsConstants.CollectionName,
            BookingsPermissions.ManageBookings)
    {
        var S = stringLocalizer;
        var booking = S["Booking"].Value;
        var order = S["Order"].Value;
        var buyer = S["Buyer"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var inCurrency = S["In the booking's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Booking ID"], DataFieldType.Text, record => record.ItemId, booking).Identifier();
        AddField(nameof(Booking.Status), S["Status"], DataFieldType.Text, record => record.Status, booking);
        AddField(nameof(Booking.Kind), S["Kind"], DataFieldType.Text, record => record.Kind, booking).Description = S["Departure, Nightly or TimeSlot."].Value;
        AddField(ProductIdField, S["Product ID"], DataFieldType.Text, record => record.ContentItemId, booking)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField(nameof(Booking.Title), S["Product"], DataFieldType.Text, record => record.Title, booking);
        AddField(nameof(Booking.OccupancyLabel), S["Occupancy"], DataFieldType.Text, record => record.OccupancyLabel, booking);
        AddField(nameof(Booking.Units), S["Units"], DataFieldType.Integer, record => record.Units, booking).Description = S["How many units were booked, such as rooms."].Value;
        AddField(nameof(Booking.People), S["People"], DataFieldType.Integer, record => record.People, booking);
        AddField(nameof(Booking.Seats), S["Seats"], DataFieldType.Integer, record => record.Seats, booking).Description = S["How much of each date it takes: seats for a departure, units otherwise."].Value;
        AddField("DateCount", S["Dates taken"], DataFieldType.Integer, record => record.SlotKeys?.Count ?? 0, booking).Description = S["The dates it takes: one for a departure or a time slot, one per night of a stay."].Value;
        AddField("TravelerCount", S["Travelers named"], DataFieldType.Integer, record => record.Travelers?.Count ?? 0, booking).Description = S["How many travelers' details the buyer gave."].Value;

        AddField(nameof(Booking.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, order)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(Booking.OrderNumber), S["Order number"], DataFieldType.Text, record => record.OrderNumber, order);
        AddField(nameof(Booking.LineId), S["Order line ID"], DataFieldType.Text, record => record.LineId, order)
            .Identifier(new DataFieldReference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.OrderLines, nameof(OrderLine.LineId)));
        AddField(nameof(Booking.CartId), S["Cart ID"], DataFieldType.Text, record => record.CartId, order)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CartSource.Name, CommerceReportDataSets.CartSource.Carts));

        AddField(nameof(Booking.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, buyer).Identifier(ReportDataReferences.User());

        AddField(nameof(Booking.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField(nameof(Booking.Total), S["Price"], DataFieldType.Decimal, record => record.Total, money).Description = inCurrency;
        AddField(nameof(Booking.CancellationFee), S["Cancellation fee"], DataFieldType.Decimal, record => record.CancellationFee, money).Description = S["What the cancellation policy kept, in the booking's currency; empty unless it was canceled."].Value;
        AddField(nameof(Booking.Refunded), S["Refunded"], DataFieldType.Decimal, record => record.Refunded, money).Description = S["What was refunded when it was canceled, in the booking's currency; empty unless it was canceled."].Value;

        AddField(CreatedUtcField, S["Booked"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Booking.StartsUtc), S["Starts"], DataFieldType.DateTime, record => record.StartsUtc, dates);
        AddField(nameof(Booking.EndsUtc), S["Ends"], DataFieldType.DateTime, record => record.EndsUtc, dates).Description = S["A stay's check-out; empty for other bookings."].Value;
        AddField(nameof(Booking.HoldExpiresUtc), S["Hold ends"], DataFieldType.DateTime, record => record.HoldExpiresUtc, dates);
        AddField(nameof(Booking.ConfirmedUtc), S["Confirmed"], DataFieldType.DateTime, record => record.ConfirmedUtc, dates);
        AddField(nameof(Booking.CompletedUtc), S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, dates);
        AddField(nameof(Booking.CanceledUtc), S["Canceled"], DataFieldType.DateTime, record => record.CanceledUtc, dates);
        AddField(nameof(Booking.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<BookingIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<BookingIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<BookingIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(Booking.OrderId)] = index => index.OrderId,
            [ProductIdField] = index => index.ContentItemId,
            [nameof(Booking.CartId)] = index => index.CartId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<BookingIndex> columns)
    {
        columns.Enum(nameof(Booking.Status), index => index.Status);
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps at most the first 255 characters of the title, so it is not always the field's value. The
        // product is kept under the content item's name.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(Booking.Title));
        columns[ProductIdField] = nameof(BookingIndex.ContentItemId);

        return columns;
    }
}
