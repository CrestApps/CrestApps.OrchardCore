using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Agreements.Models;

/// <summary>
/// An agreement with an account, such as a support contract or a maintenance contract: its period, where it stands and
/// the hours it covers. Other modules keep their own terms in its <c>Properties</c> (Work Orders' visits, the service
/// level of tickets) and add their editors through display drivers.
/// </summary>
public sealed class Agreement : CatalogItem
{
    /// <summary>
    /// Gets or sets the number, such as SA-00042, from a locked sequence.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public AgreementStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the account it is with.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets its first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets its last day, or <see langword="null"/> when it runs until it is canceled.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the time zone its days and months are counted in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the hours of work it covers.
    /// </summary>
    public AgreementHours Hours { get; set; } = new();

    /// <summary>
    /// Gets or sets how many days before its end observers are told it is about to end.
    /// </summary>
    public int ExpiringNoticeDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets when observers were told it is about to end, in UTC, so they are told once.
    /// </summary>
    public DateTime? ExpiringNotifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the shares of the hours, in percent, observers were told about in the current period, and the start
    /// of that period, so each share is told once a period.
    /// </summary>
    public AgreementHoursWarnings HoursWarnings { get; set; } = new();

    /// <summary>
    /// Gets or sets notes for the people who work under it.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets when it was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets whether it is in force on a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>Whether it is active and the day is in its period.</returns>
    public bool IsInForceOn(DateOnly date)
        => Status == AgreementStatus.Active && date >= StartDate && (EndDate is null || date <= EndDate);
}
