using System.Globalization;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Exports and anonymizes the bookings a person made, with their account or as a guest, and the balances still owed on
/// them. A booking keeps its dates, units and amounts, which the store must keep; the booker's name, email, travelers
/// and the reason they gave for canceling go.
/// </summary>
public sealed class BookingPersonalDataContributor : IPersonalDataContributor
{
    /// <summary>The record kind of a booking.</summary>
    public const string BookingKind = "Booking";

    /// <summary>The record kind of a booking balance.</summary>
    public const string BalanceKind = "BookingBalance";

    private readonly IBookingStore _bookings;
    private readonly IBookingBalanceStore _balances;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="bookings">The booking store.</param>
    /// <param name="balances">The booking balance store.</param>
    public BookingPersonalDataContributor(
        IBookingStore bookings,
        IBookingBalanceStore balances)
    {
        _bookings = bookings;
        _balances = balances;
    }

    /// <inheritdoc/>
    public async Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        // Only the name, for scrubbing free text: an email or a phone number on a record may be shared by other people,
        // so adding it would lead the other contributors to their records.
        foreach (var booking in await _bookings.ListForPersonAsync(subject.UserIds, subject.Emails, cancellationToken))
        {
            subject.AddName(booking.CustomerName);
        }
    }

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var booking in await _bookings.ListForPersonAsync(context.Subject.UserIds, context.Subject.Emails, cancellationToken))
        {
            var record = context.Add(BookingKind, booking.ItemId, booking.Title, booking.CreatedUtc);

            record.Fields[nameof(Booking.OrderNumber)] = booking.OrderNumber;
            record.Fields[nameof(Booking.StartsUtc)] = booking.StartsUtc.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(Booking.EndsUtc)] = booking.EndsUtc?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(Booking.Status)] = booking.Status.ToString();
            record.Fields[nameof(Booking.People)] = booking.People.ToString(CultureInfo.InvariantCulture);
            record.Fields[nameof(Booking.CustomerName)] = booking.CustomerName;
            record.Fields[nameof(Booking.CustomerEmail)] = booking.CustomerEmail;
            record.Fields[nameof(Booking.Travelers)] = booking.Travelers?.Count > 0
                ? string.Join(Environment.NewLine, booking.Travelers.Select(traveler => traveler.ToJsonString()))
                : null;
            record.Fields[nameof(Booking.CancelReason)] = booking.CancelReason;

            var balance = await _balances.FindByOrderAsync(booking.OrderId, cancellationToken);

            if (balance is not null && context.Export.Records.All(existing => existing.Kind != BalanceKind || existing.Id != balance.ItemId))
            {
                var balanceRecord = context.Add(BalanceKind, balance.ItemId, balance.OrderNumber, balance.CreatedUtc);

                balanceRecord.Fields[nameof(BookingBalance.Amount)] = balance.Amount.ToString(CultureInfo.InvariantCulture);
                balanceRecord.Fields[nameof(BookingBalance.Currency)] = balance.Currency;
                balanceRecord.Fields[nameof(BookingBalance.DueUtc)] = balance.DueUtc.ToString("O", CultureInfo.InvariantCulture);
                balanceRecord.Fields[nameof(BookingBalance.Status)] = balance.Status.ToString();
                balanceRecord.Fields[nameof(BookingBalance.CustomerName)] = balance.CustomerName;
                balanceRecord.Fields[nameof(BookingBalance.CustomerEmail)] = balance.CustomerEmail;
            }
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var balancesDone = new HashSet<string>(StringComparer.Ordinal);

        foreach (var booking in await _bookings.ListForPersonAsync(context.Subject.UserIds, context.Subject.Emails, cancellationToken))
        {
            booking.CustomerName = context.ReplaceName(booking.CustomerName);
            booking.CustomerEmail = null;
            booking.Travelers = [];
            booking.CancelReason = context.Scrub(booking.CancelReason);

            await _bookings.UpdateAsync(booking, cancellationToken);
            context.Result.Count(BookingKind);

            var balance = await _balances.FindByOrderAsync(booking.OrderId, cancellationToken);

            if (balance is null || !balancesDone.Add(balance.ItemId))
            {
                continue;
            }

            balance.CustomerName = context.ReplaceName(balance.CustomerName);
            balance.CustomerEmail = null;

            await _balances.UpdateAsync(balance, cancellationToken);
            context.Result.Count(BalanceKind);
        }
    }
}
