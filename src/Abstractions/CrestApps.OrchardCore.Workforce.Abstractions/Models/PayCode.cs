using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A code hours are paid under, such as regular, overtime or a night premium.
/// </summary>
public sealed class PayCode : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the short code, unique, such as REG or OT.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the code pays for.
    /// </summary>
    public PayCodeKind Kind { get; set; } = PayCodeKind.Regular;

    /// <summary>
    /// Gets or sets what the hourly rate is multiplied by, such as 1.5 for overtime.
    /// </summary>
    public decimal Multiplier { get; set; } = 1;

    /// <summary>
    /// Gets or sets the premium an hour, for a shift differential.
    /// </summary>
    public decimal? PremiumPerHour { get; set; }

    /// <summary>
    /// Gets or sets when a shift differential's window starts, in the worker's time.
    /// </summary>
    public TimeOnly? WindowStart { get; set; }

    /// <summary>
    /// Gets or sets when a shift differential's window ends; before the start for a window past midnight.
    /// </summary>
    public TimeOnly? WindowEnd { get; set; }

    /// <summary>
    /// Gets or sets the days a shift differential applies on; empty for every day.
    /// </summary>
    public IList<DayOfWeek> Weekdays { get; set; } = [];

    /// <summary>
    /// Gets or sets the code payroll knows it by, when it differs.
    /// </summary>
    public string ExportCode { get; set; }

    /// <summary>
    /// Gets or sets whether the code is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the code to export.
    /// </summary>
    public string PayrollCode => string.IsNullOrEmpty(ExportCode) ? Code : ExportCode;
}
