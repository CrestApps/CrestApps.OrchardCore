using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Tells the buyer about their booking: that it's confirmed, with when and what's due, and that it's canceled, with
/// what's refunded. Each passes through the store's email layout.
/// </summary>
public sealed class BookingEmailSender
{
    internal const string EmailKind = "booking";

    private readonly ICommerceEmailSender _emailSender;
    private readonly ISiteService _siteService;
    private readonly HtmlEncoder _encoder;

    internal readonly IStringLocalizer S;

    public BookingEmailSender(
        ICommerceEmailSender emailSender,
        ISiteService siteService,
        HtmlEncoder encoder,
        IStringLocalizer<BookingEmailSender> stringLocalizer)
    {
        _emailSender = emailSender;
        _siteService = siteService;
        _encoder = encoder;
        S = stringLocalizer;
    }

    public Task<bool> SendConfirmedAsync(Order order, IReadOnlyList<Booking> bookings, BookingBalance balance, TimeZoneInfo timeZone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookings);

        return SendAsync(order, (bookings.Count > 0 ? bookings[0].CustomerEmail : null) ?? order?.CustomerEmail, (bookings.Count > 0 ? bookings[0].CustomerName : null) ?? order?.CustomerName, () =>
        {
            var lines = bookings.Select(booking => Describe(booking, timeZone)).ToList();

            if (balance is { Status: BookingBalanceStatus.Scheduled })
            {
                lines.Add(S["The rest, {0}, is charged to the card you paid with on {1}. You can pay it sooner from your account.", ProductPriceFormatter.Format(balance.Amount, balance.Currency), ToLocal(balance.DueUtc, timeZone).ToString("D", CultureInfo.CurrentCulture)].Value);
            }

            return (S["Your booking is confirmed: order {0}", order?.OrderNumber].Value, S["Your booking is confirmed. Here's what you booked:"].Value, lines);
        }, cancellationToken);
    }

    public Task<bool> SendCanceledAsync(Order order, Booking booking, decimal refunded, TimeZoneInfo timeZone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(booking);

        return SendAsync(order, booking.CustomerEmail ?? order?.CustomerEmail, booking.CustomerName ?? order?.CustomerName, () =>
        {
            var lines = new List<string> { Describe(booking, timeZone) };

            lines.Add(refunded > 0m
                ? S["{0} is refunded to how you paid. It can take a few days to show.", ProductPriceFormatter.Format(refunded, booking.Currency)].Value
                : S["Under the cancellation policy, nothing is refunded."].Value);

            if (!string.IsNullOrWhiteSpace(booking.CancelReason))
            {
                lines.Add(booking.CancelReason);
            }

            return (S["Your booking is canceled: order {0}", order?.OrderNumber ?? booking.OrderNumber].Value, S["Your booking is canceled."].Value, lines);
        }, cancellationToken);
    }

    internal string Describe(Booking booking, TimeZoneInfo timeZone)
    {
        var culture = CultureInfo.CurrentCulture;
        var start = ToLocal(booking.StartsUtc, timeZone);
        var when = booking.Kind switch
        {
            BookingKind.Nightly => S["{0} – {1}", start.ToString("D", culture), ToLocal(booking.EndsUtc ?? booking.StartsUtc, timeZone).ToString("D", culture)].Value,
            BookingKind.TimeSlot => start.ToString("f", culture),
            _ => start.ToString("D", culture),
        };
        var who = string.IsNullOrEmpty(booking.OccupancyLabel)
            ? S.Plural(booking.People, "1 person", "{0} people").Value
            : S["{0} × {1}", booking.Units, booking.OccupancyLabel].Value;

        return S["{0}: {1}, {2}", booking.Title, when, who].Value;
    }

    // Written in the language the buyer shopped in: the subject, the intro and the lines are described in it.
    private Task<bool> SendAsync(Order order, string to, string name, Func<(string Subject, string Intro, List<string> Lines)> describe, CancellationToken cancellationToken)
        => _emailSender.SendAsync(new CommerceEmail
        {
            Kind = EmailKind,
            To = to,
            Order = order,
            Culture = order?.Culture,
            Reference = order?.OrderNumber,
        }, _ => BuildAsync(name, describe), cancellationToken);

    private async Task<CommerceEmailContent> BuildAsync(string name, Func<(string Subject, string Intro, List<string> Lines)> describe)
    {
        var (subject, intro, lines) = describe();
        var site = await _siteService.GetSiteSettingsAsync();
        var greeting = string.IsNullOrWhiteSpace(name) ? S["Hello,"].Value : S["Hello {0},", name].Value;
        var text = new StringBuilder().AppendLine(greeting).AppendLine().AppendLine(intro).AppendLine();
        var html = new StringBuilder()
            .Append("<p>").Append(_encoder.Encode(greeting)).Append("</p>")
            .Append("<p>").Append(_encoder.Encode(intro)).Append("</p><ul>");

        foreach (var line in lines)
        {
            text.Append("- ").AppendLine(line);
            html.Append("<li>").Append(_encoder.Encode(line)).Append("</li>");
        }

        html.Append("</ul><p>").Append(_encoder.Encode(site.SiteName)).Append("</p>");
        text.AppendLine().AppendLine(site.SiteName);

        return new CommerceEmailContent
        {
            Subject = subject,
            Text = text.ToString(),
            Html = html.ToString(),
        };
    }

    internal static DateTime ToLocal(DateTime utc, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone ?? TimeZoneInfo.Utc);
}
