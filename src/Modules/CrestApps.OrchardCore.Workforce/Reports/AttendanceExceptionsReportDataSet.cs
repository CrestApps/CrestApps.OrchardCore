using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Workforce.Reports;

/// <summary>
/// One row per attendance exception: its kind, day, minutes, points and whether it is open, confirmed or excused.
/// </summary>
public sealed class AttendanceExceptionsReportDataSet : WorkforceReportDataSet<AttendanceException, AttendanceExceptionIndex>
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforceAttendanceExceptions";

    /// <summary>
    /// Initializes a new instance of the <see cref="AttendanceExceptionsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AttendanceExceptionsReportDataSet(ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, IStringLocalizer<AttendanceExceptionsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Attendance exceptions"], stringLocalizer["One row per attendance exception: its kind, day, minutes, points and whether it is open, confirmed or excused."]), session, authorizationService, scopeService, WorkforcePermissions.ApproveTimesheets)
    {
        var S = stringLocalizer;
        var group = S["Attendance exceptions"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, row => row.Record.ItemId, group).Identifier();
        AddField("WorkerId", S["Worker ID"], DataFieldType.Text, row => row.Record.WorkerId, group);
        AddField("WorkerName", S["Worker"], DataFieldType.Text, row => row.WorkerName, group);
        AddField("TeamName", S["Team"], DataFieldType.Text, row => row.TeamName, group);
        AddField("Kind", S["Exception"], DataFieldType.Text, row => row.Record.Kind.ToString(), group);
        AddField("Date", S["Day"], DataFieldType.Text, row => row.Record.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), group);
        AddField("Minutes", S["Minutes"], DataFieldType.Integer, row => row.Record.Minutes, group);
        AddField("Points", S["Points"], DataFieldType.Decimal, row => row.Record.Points, group);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Record.Status.ToString(), group);
        AddField("CreatedUtc", S["Raised"], DataFieldType.DateTime, row => row.Record.CreatedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<AttendanceExceptionIndex, DateTime>> Column)? DateColumn => ("CreatedUtc", index => index.CreatedUtc);

    /// <inheritdoc />
    protected override Expression<Func<AttendanceExceptionIndex, string>> TeamColumn => index => index.TeamId;

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<AttendanceExceptionIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AttendanceExceptionIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["WorkerId"] = index => index.WorkerId,
        };

    /// <inheritdoc />
    protected override string WorkerOf(AttendanceException record) => record.WorkerId;

    /// <inheritdoc />
    protected override string TeamOf(AttendanceException record) => record.TeamId;
}
