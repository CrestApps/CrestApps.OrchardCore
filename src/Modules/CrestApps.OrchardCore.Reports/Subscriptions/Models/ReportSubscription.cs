using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Reports.Subscriptions.Models;

/// <summary>
/// A report emailed on a schedule, such as the sales report every Monday morning for the week before.
/// </summary>
public sealed class ReportSubscription : CatalogItem
{
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the report, such as <c>order-sales</c>.
    /// </summary>
    public string ReportName { get; set; }

    public ReportSubscriptionFrequency Frequency { get; set; } = ReportSubscriptionFrequency.Weekly;

    /// <summary>
    /// Gets or sets the day a weekly report goes out.
    /// </summary>
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    /// <summary>
    /// Gets or sets the day of the month a monthly report goes out, from 1 to 28.
    /// </summary>
    public int DayOfMonth { get; set; } = 1;

    /// <summary>
    /// Gets or sets the hour it goes out, from 0 to 23, in the site's time zone.
    /// </summary>
    public int Hour { get; set; } = 7;

    /// <summary>
    /// Gets or sets the email addresses it goes to, separated by commas.
    /// </summary>
    public string Recipients { get; set; }

    /// <summary>
    /// Gets or sets the format of the attached file, such as <c>csv</c>, or none for no attachment.
    /// </summary>
    public string AttachmentFormat { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? NextRunUtc { get; set; }

    public DateTime? LastRunUtc { get; set; }

    /// <summary>
    /// Gets or sets why the last run didn't go out, or none when it did.
    /// </summary>
    public string LastError { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }
}

public enum ReportSubscriptionFrequency
{
    /// <summary>
    /// Every day, for the day before.
    /// </summary>
    Daily,

    /// <summary>
    /// Every week on its day, for the seven days before.
    /// </summary>
    Weekly,

    /// <summary>
    /// Every month on its day, for the month before.
    /// </summary>
    Monthly,
}
