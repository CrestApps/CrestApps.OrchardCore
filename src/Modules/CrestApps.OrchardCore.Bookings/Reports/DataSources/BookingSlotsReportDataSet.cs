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
/// One row per date of a booking product, such as a departure, a night or a time slot: how much of it there is, how
/// much unpaid orders hold and paid bookings take, and whether it can still be booked. A date's own price is in the
/// product's currency, which the date doesn't record, so only whether it has one is exposed.
/// </summary>
public sealed class BookingSlotsReportDataSet : IndexedReportDataSet<BookingSlot, BookingSlotIndex>, IBookingReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the date starts, the data set's main date.
    /// </summary>
    public const string StartsUtcField = "StartsUtc";

    /// <summary>
    /// The name of the field that holds the product the date belongs to.
    /// </summary>
    public const string ProductIdField = "ProductId";

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingSlotsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session dates are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingSlotsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<BookingSlotsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.BookingSource.Slots,
                stringLocalizer["Booking dates"],
                stringLocalizer["One row per date of a booking product, such as a departure, a night or a time slot: its capacity, what is held, sold and left, and whether it is closed."]),
            session,
            authorizationService,
            BookingsConstants.CollectionName,
            BookingsPermissions.ManageBookings)
    {
        var S = stringLocalizer;
        var date = S["Date"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Date ID"], DataFieldType.Text, record => record.ItemId, date).Identifier();
        AddField(ProductIdField, S["Product ID"], DataFieldType.Text, record => record.ContentItemId, date)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField(nameof(BookingSlot.SlotKey), S["Date or time"], DataFieldType.Text, record => record.SlotKey, date).Description = S["The date (yyyy-MM-dd) or the time (yyyy-MM-ddTHH:mm), in the site's time zone."].Value;
        AddField(StartsUtcField, S["Starts"], DataFieldType.DateTime, record => record.StartsUtc, date);
        AddField(nameof(BookingSlot.Capacity), S["Capacity"], DataFieldType.Integer, record => record.Capacity, date);
        AddField(nameof(BookingSlot.Held), S["Held"], DataFieldType.Integer, record => record.Held, date).Description = S["How much unpaid orders hold."].Value;
        AddField(nameof(BookingSlot.Sold), S["Sold"], DataFieldType.Integer, record => record.Sold, date).Description = S["How much paid bookings take."].Value;
        AddField(nameof(BookingSlot.Remaining), S["Left"], DataFieldType.Integer, record => record.Remaining, date);
        AddField(nameof(BookingSlot.Closed), S["Closed"], DataFieldType.Boolean, record => record.Closed, date);
        AddField("HasOwnPrice", S["Has its own price"], DataFieldType.Boolean, record => record.Price.HasValue, date).Description = S["Whether the date has its own price in place of the product's."].Value;
        AddField(nameof(BookingSlot.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, date);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<BookingSlotIndex, DateTime>> Column)? DateColumn
        => (StartsUtcField, index => index.StartsUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<BookingSlotIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<BookingSlotIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [ProductIdField] = index => index.ContentItemId,
        };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The product is kept under the content item's name.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal)
        {
            [ProductIdField] = nameof(BookingSlotIndex.ContentItemId),
        };

        return columns;
    }
}
