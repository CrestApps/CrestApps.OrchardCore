using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps TimeClockPolicy records to <see cref="TimeClockPolicyIndex"/> rows.
/// </summary>
public sealed class TimeClockPolicyIndexProvider : IndexProvider<TimeClockPolicy>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockPolicyIndexProvider"/> class.
    /// </summary>
    public TimeClockPolicyIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TimeClockPolicy> context)
    {
        context.For<TimeClockPolicyIndex>()
            .Map(item => new TimeClockPolicyIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Enabled = item.Enabled,
            });
    }
}

/// <summary>
/// Maps PayPeriodCalendar records to <see cref="PayPeriodCalendarIndex"/> rows.
/// </summary>
public sealed class PayPeriodCalendarIndexProvider : IndexProvider<PayPeriodCalendar>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayPeriodCalendarIndexProvider"/> class.
    /// </summary>
    public PayPeriodCalendarIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayPeriodCalendar> context)
    {
        context.For<PayPeriodCalendarIndex>()
            .Map(item => new PayPeriodCalendarIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Enabled = item.Enabled,
            });
    }
}

/// <summary>
/// Maps PayCode records to <see cref="PayCodeIndex"/> rows.
/// </summary>
public sealed class PayCodeIndexProvider : IndexProvider<PayCode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayCodeIndexProvider"/> class.
    /// </summary>
    public PayCodeIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayCode> context)
    {
        context.For<PayCodeIndex>()
            .Map(item => new PayCodeIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Code = item.Code,
                Kind = item.Kind,
                Enabled = item.Enabled,
            });
    }
}

/// <summary>
/// Maps TimeClockKiosk records to <see cref="TimeClockKioskIndex"/> rows.
/// </summary>
public sealed class TimeClockKioskIndexProvider : IndexProvider<TimeClockKiosk>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockKioskIndexProvider"/> class.
    /// </summary>
    public TimeClockKioskIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TimeClockKiosk> context)
    {
        context.For<TimeClockKioskIndex>()
            .Map(item => new TimeClockKioskIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                TokenHash = item.TokenHash,
                Enabled = item.Enabled,
            });
    }
}

/// <summary>
/// Maps TimePunch records to <see cref="TimePunchIndex"/> rows.
/// </summary>
public sealed class TimePunchIndexProvider : IndexProvider<TimePunch>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimePunchIndexProvider"/> class.
    /// </summary>
    public TimePunchIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TimePunch> context)
    {
        context.For<TimePunchIndex>()
            .Map(item => new TimePunchIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                TeamId = item.TeamId,
                Kind = item.Kind,
                Utc = item.Utc,
                IdempotencyKey = item.IdempotencyKey,
                ReplacesPunchId = item.ReplacesPunchId,
            });
    }
}

/// <summary>
/// Maps Timesheet records to <see cref="TimesheetIndex"/> rows.
/// </summary>
public sealed class TimesheetIndexProvider : IndexProvider<Timesheet>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimesheetIndexProvider"/> class.
    /// </summary>
    public TimesheetIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Timesheet> context)
    {
        context.For<TimesheetIndex>()
            .Map(item => new TimesheetIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                TeamId = item.TeamId,
                PayPeriodCalendarId = item.PayPeriodCalendarId,
                PeriodStart = WorkforceDates.ToKey(item.PeriodStart),
                PeriodEnd = WorkforceDates.ToKey(item.PeriodEnd),
                Status = item.Status,
            });
    }
}

/// <summary>
/// Maps AttendanceException records to <see cref="AttendanceExceptionIndex"/> rows.
/// </summary>
public sealed class AttendanceExceptionIndexProvider : IndexProvider<AttendanceException>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttendanceExceptionIndexProvider"/> class.
    /// </summary>
    public AttendanceExceptionIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<AttendanceException> context)
    {
        context.For<AttendanceExceptionIndex>()
            .Map(item => new AttendanceExceptionIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                TeamId = item.TeamId,
                Kind = item.Kind,
                Status = item.Status,
                Date = WorkforceDates.ToKey(item.Date),
                ExceptionKey = item.Key,
                CreatedUtc = item.CreatedUtc,
            });
    }
}

/// <summary>
/// Maps PunchCorrection records to <see cref="PunchCorrectionIndex"/> rows.
/// </summary>
public sealed class PunchCorrectionIndexProvider : IndexProvider<PunchCorrection>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PunchCorrectionIndexProvider"/> class.
    /// </summary>
    public PunchCorrectionIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PunchCorrection> context)
    {
        context.For<PunchCorrectionIndex>()
            .Map(item => new PunchCorrectionIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                TeamId = item.TeamId,
                Status = item.Status,
                CreatedUtc = item.CreatedUtc,
            });
    }
}

/// <summary>
/// Maps PayrollExportBatch records to <see cref="PayrollExportBatchIndex"/> rows.
/// </summary>
public sealed class PayrollExportBatchIndexProvider : IndexProvider<PayrollExportBatch>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollExportBatchIndexProvider"/> class.
    /// </summary>
    public PayrollExportBatchIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayrollExportBatch> context)
    {
        context.For<PayrollExportBatchIndex>()
            .Map(item => new PayrollExportBatchIndex
            {
                ItemId = item.ItemId,
                ProviderName = item.ProviderName,
                PayPeriodCalendarId = item.PayPeriodCalendarId,
                PeriodStart = WorkforceDates.ToKey(item.PeriodStart),
                CreatedUtc = item.CreatedUtc,
            });
    }
}

/// <summary>
/// The time clock's index providers, for hosts and tests that register them by hand.
/// </summary>
public static class TimeClockIndexProviders
{
    /// <summary>
    /// Returns a new instance of each provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
    {
        return
        [
            new TimeClockPolicyIndexProvider(),
            new PayPeriodCalendarIndexProvider(),
            new PayCodeIndexProvider(),
            new TimeClockKioskIndexProvider(),
            new TimePunchIndexProvider(),
            new TimesheetIndexProvider(),
            new AttendanceExceptionIndexProvider(),
            new PunchCorrectionIndexProvider(),
            new PayrollExportBatchIndexProvider(),
        ];
    }
}
