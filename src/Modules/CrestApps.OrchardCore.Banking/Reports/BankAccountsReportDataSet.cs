using System.Linq.Expressions;
using CrestApps.OrchardCore.Banking.Core.Indexes;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Banking.Reports;

/// <summary>
/// One row per bank account: who owns it, the bank, the status and the last digits of the number. The routing and
/// account numbers are never columns.
/// </summary>
public sealed class BankAccountsReportDataSet : IndexedReportDataSet<BankAccount, BankAccountIndex>, IBankingReportDataSet
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "BankAccounts";

    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BankAccountsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<BankAccountsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(DataSetName, stringLocalizer["Bank accounts"], stringLocalizer["One row per bank account: the owner, the bank, the status and the last digits of the account number."]),
            session,
            authorizationService,
            BankingConstants.CollectionName,
            BankingPermissions.ManageBankAccounts)
    {
        var S = stringLocalizer;
        var group = S["Bank account"].Value;
        var dates = S["Dates"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Name", S["Name"], DataFieldType.Text, record => record.Name, group);
        AddField("OwnerKind", S["Owner kind"], DataFieldType.Text, record => record.Owner.IsEmpty ? null : record.Owner.Kind, group);
        AddField("OwnerId", S["Owner ID"], DataFieldType.Text, record => record.Owner.IsEmpty ? null : record.Owner.Id, group);
        AddField("HolderName", S["Account holder"], DataFieldType.Text, record => record.HolderName, group);
        AddField("Kind", S["Account type"], DataFieldType.Text, record => record.Kind.ToString(), group);
        AddField("BankName", S["Bank"], DataFieldType.Text, record => record.BankName, group);
        AddField("Country", S["Country"], DataFieldType.Text, record => record.Country, group);
        AddField("Currency", S["Currency"], DataFieldType.Text, record => record.Currency, group);
        AddField("AccountNumberLastDigits", S["Account number (last digits)"], DataFieldType.Text, record => BankAccountNumbers.LastDigits(record), group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group).Description = S["Unverified, VerificationSent, Verified, Failed or Closed."].Value;
        AddField("VerificationMethod", S["Verified by"], DataFieldType.Text, record => record.VerificationMethod, group);
        AddField("CreatedUtc", S["Added"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField("VerifiedUtc", S["Verified"], DataFieldType.DateTime, record => record.VerifiedUtc, dates);
        AddField("NumberChangedUtc", S["Number changed"], DataFieldType.DateTime, record => record.NumberChangedUtc, dates);
        AddField("StatusChangedUtc", S["Status changed"], DataFieldType.DateTime, record => record.StatusChangedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<BankAccountIndex, DateTime>> Column)? DateColumn
        => ("CreatedUtc", index => index.CreatedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<BankAccountIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<BankAccountIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["OwnerId"] = index => index.OwnerId,
        };

    /// <inheritdoc />
    protected override void DescribeColumns(ReportIndexConditions<BankAccountIndex> columns)
    {
        columns.Text("Status", nameof(BankAccountIndex.Status));
    }
}
