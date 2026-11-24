using CrestApps.OrchardCore.Learning.Certifications.Models;

namespace CrestApps.OrchardCore.Learning.Certifications.Core;

/// <summary>
/// Works out certificate expiry by the path's reference date, and stores days as sortable numbers.
/// </summary>
public static class CertificationDates
{
    /// <summary>
    /// Turns a day into a sortable number, yyyyMMdd.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The number, or <see langword="null"/>.</returns>
    public static int? ToKey(DateOnly? date) => date is { } value ? (value.Year * 10000) + (value.Month * 100) + value.Day : null;

    /// <summary>
    /// Works out the last day a certificate is valid.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="validityMonths">How many months a certificate lasts; <see langword="null"/> when it never expires.</param>
    /// <param name="issuedOn">The day it is issued.</param>
    /// <param name="previousExpiresOn">The expiry of the certificate a renewal replaces, or <see langword="null"/>.</param>
    /// <returns>The expiry, or <see langword="null"/> when it never expires.</returns>
    public static DateOnly? Expiry(CertificationPath path, int? validityMonths, DateOnly issuedOn, DateOnly? previousExpiresOn)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (validityMonths is not { } months || months <= 0)
        {
            return null;
        }

        switch (path.ReferenceDate)
        {
            case CertificationReferenceDate.ExpiryDate when previousExpiresOn is { } previous:
                // The anniversary is kept however early the renewal was done; a very late one moves on by whole periods.
                var next = previous.AddMonths(months);

                while (next <= issuedOn)
                {
                    next = next.AddMonths(months);
                }

                return next;

            case CertificationReferenceDate.FixedDate:
                var earliest = previousExpiresOn is { } before
                    ? before.AddMonths(months)
                    : issuedOn.AddMonths(Math.Max(0, path.MinimumIntervalMonths));

                return NextFixedDate(path, earliest);

            default:
                return issuedOn.AddMonths(months).AddDays(-1);
        }
    }

    /// <summary>
    /// Finds the first fixed expiry day on or after a day.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="onOrAfter">The earliest day.</param>
    /// <returns>The day.</returns>
    public static DateOnly NextFixedDate(CertificationPath path, DateOnly onOrAfter)
    {
        var month = Math.Clamp(path.FixedMonth, 1, 12);

        for (var year = onOrAfter.Year; ; year++)
        {
            var day = Math.Clamp(path.FixedDay, 1, DateTime.DaysInMonth(year, month));
            var candidate = new DateOnly(year, month, day);

            if (candidate >= onOrAfter)
            {
                return candidate;
            }
        }
    }
}
