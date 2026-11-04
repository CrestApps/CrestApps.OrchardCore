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
/// One row per predictability pay entry: the change, the notice it came with, the premium and whether it was approved, waived or exported.
/// </summary>
public sealed class PredictabilityPayReportDataSet : WorkforceReportDataSet<PredictabilityPayEntry, PredictabilityPayEntryIndex>
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforcePredictabilityPay";

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictabilityPayReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PredictabilityPayReportDataSet(ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, IStringLocalizer<PredictabilityPayReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Predictability pay"], stringLocalizer["One row per predictability pay entry: the change, the notice it came with, the premium and whether it was approved, waived or exported."]), session, authorizationService, scopeService, WorkforcePermissions.ApprovePredictabilityPay)
    {
        var S = stringLocalizer;
        var group = S["Predictability pay"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, row => row.Record.ItemId, group).Identifier();
        AddField("WorkerId", S["Worker ID"], DataFieldType.Text, row => row.Record.WorkerId, group);
        AddField("WorkerName", S["Worker"], DataFieldType.Text, row => row.WorkerName, group);
        AddField("TeamName", S["Team"], DataFieldType.Text, row => row.TeamName, group);
        AddField("ChangeKind", S["Change"], DataFieldType.Text, row => row.Record.ChangeKind.ToString(), group);
        AddField("ShiftStartUtc", S["Shift start"], DataFieldType.DateTime, row => row.Record.ShiftStartUtc, group);
        AddField("NoticeHours", S["Notice (hours)"], DataFieldType.Decimal, row => row.Record.NoticeHours, group);
        AddField("Hours", S["Premium (hours)"], DataFieldType.Decimal, row => row.Record.Hours, group);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Record.Status.ToString(), group);
        AddField("CreatedUtc", S["Recorded"], DataFieldType.DateTime, row => row.Record.CreatedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<PredictabilityPayEntryIndex, DateTime>> Column)? DateColumn => ("CreatedUtc", index => index.CreatedUtc);

    /// <inheritdoc />
    protected override Expression<Func<PredictabilityPayEntryIndex, string>> TeamColumn => index => index.TeamId;

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<PredictabilityPayEntryIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<PredictabilityPayEntryIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["WorkerId"] = index => index.WorkerId,
        };

    /// <inheritdoc />
    protected override string WorkerOf(PredictabilityPayEntry record) => record.WorkerId;

    /// <inheritdoc />
    protected override string TeamOf(PredictabilityPayEntry record) => record.TeamId;
}
