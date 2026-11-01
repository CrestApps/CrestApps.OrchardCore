using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

public sealed class AttendanceExceptionIndex : CatalogItemIndex
{
    public string WorkerId { get; set; }

    public string TeamId { get; set; }

    public AttendanceExceptionKind Kind { get; set; }

    public AttendanceExceptionStatus Status { get; set; }

    public int Date { get; set; }

    public string ExceptionKey { get; set; }

    public DateTime CreatedUtc { get; set; }
}
