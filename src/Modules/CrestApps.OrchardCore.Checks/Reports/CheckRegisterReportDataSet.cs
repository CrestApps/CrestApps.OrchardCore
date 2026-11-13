using System.Linq.Expressions;
using CrestApps.OrchardCore.Checks.Core.Indexes;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.YesSql.Core.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Checks.Reports;

/// <summary>
/// One row per check: bank account, number, payee, amount, date, status and dates. It never holds an account number.
/// </summary>
public sealed class CheckRegisterReportDataSet : IndexedReportDataSet<CheckRecord, CheckRecordIndex>, IChecksReportDataSet
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "CheckRegister";

    private readonly ISourceAccessService _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckRegisterReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Who may see the checks of each source.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckRegisterReportDataSet(ISession session, IAuthorizationService authorizationService, ISourceAccessService access, IStringLocalizer<CheckRegisterReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(DataSetName, stringLocalizer["Check register"], stringLocalizer["One row per check: bank account, number, payee, amount, check date and status, for reports and the bank's positive pay file."]),
            session,
            authorizationService,
            ChecksConstants.CollectionName,
            ChecksPermissions.ViewCheckRegister)
    {
        _access = access;

        var S = stringLocalizer;
        var group = S["Check"].Value;
        var dates = S["Dates"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("BankAccountId", S["Bank account ID"], DataFieldType.Text, record => record.BankAccountId, group);
        AddField("BankAccountName", S["Bank account"], DataFieldType.Text, record => record.BankAccountName, group);
        AddField("Number", S["Check number"], DataFieldType.Integer, record => record.Number, group);
        AddField("PayeeName", S["Payee"], DataFieldType.Text, record => record.PayeeName, group);
        AddField("Amount", S["Amount"], DataFieldType.Decimal, record => record.Amount, group);
        AddField("Currency", S["Currency"], DataFieldType.Text, record => record.Currency, group);
        AddField("Memo", S["Memo"], DataFieldType.Text, record => record.Memo, group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group).Description = S["Issued, Printed, Voided, StopPayment, Cleared, Reissued or Stale."].Value;
        AddField("IsVoid", S["Void"], DataFieldType.Boolean, record => record.Status is CheckStatus.Voided or CheckStatus.Reissued or CheckStatus.StopPayment, group).Description = S["Whether the bank must refuse the check: voided, stopped or replaced."].Value;
        AddField("SourceKind", S["Source kind"], DataFieldType.Text, record => record.SourceKind, group);
        AddField("SourceId", S["Source ID"], DataFieldType.Text, record => record.SourceId, group);
        AddField("CheckDate", S["Check date"], DataFieldType.Date, record => record.CheckDate.ToDateTime(TimeOnly.MinValue), dates);
        AddField("IssuedUtc", S["Issued"], DataFieldType.DateTime, record => record.IssuedUtc, dates);
        AddField("PrintedUtc", S["Printed"], DataFieldType.DateTime, record => record.PrintedUtc, dates);
        AddField("ClosedUtc", S["Voided or stopped"], DataFieldType.DateTime, record => record.ClosedUtc, dates);
        AddField("ClearedOn", S["Cleared"], DataFieldType.Date, record => record.ClearedOn?.ToDateTime(TimeOnly.MinValue), dates);
        AddField("ModifiedUtc", S["Changed"], DataFieldType.DateTime, record => record.ModifiedUtc ?? record.IssuedUtc, dates).Description = S["When the check last changed: what the positive pay file reads new rows by."].Value;

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<CheckRecordIndex, DateTime>> Column)? DateColumn
        => ("ModifiedUtc", index => index.ModifiedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<CheckRecordIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CheckRecordIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["BankAccountId"] = index => index.BankAccountId,
            ["SourceId"] = index => index.SourceId,
        };

    /// <inheritdoc />
    protected override void DescribeColumns(ReportIndexConditions<CheckRecordIndex> columns)
    {
        columns.Text("Status", nameof(CheckRecordIndex.Status));
    }

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<CheckRecordIndex>, IQueryIndex<CheckRecordIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var filter = await _access.GetFilterAsync(context?.User, SourceAccessLevel.View, cancellationToken);

        if (SourceAccessExpressions.Restrict<CheckRecordIndex>(filter) is not { } restrict)
        {
            return rows => rows;
        }

        return rows => rows.Where(restrict);
    }
}
