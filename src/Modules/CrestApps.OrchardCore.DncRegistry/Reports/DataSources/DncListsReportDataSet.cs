using System.Linq.Expressions;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.DncRegistry.Reports.DataSources;

/// <summary>
/// The <c>DncLists</c> data set: one row per local do-not-call list, with its country, import status and progress,
/// and when it was uploaded and imported, as the local DNC registry page shows them. Only principals allowed to manage
/// the DNC registry settings read it. Where the uploaded file is stored and the text of the row errors are never
/// exposed, only how many rows failed.
/// </summary>
public sealed class DncListsReportDataSet : IndexedReportDataSet<LocalDncList, LocalDncListIndex>
{
    /// <summary>
    /// The technical name of the field that holds when the list was uploaded, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="DncListsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the lists are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DncListsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<DncListsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                DncRegistryReportDataSets.DncLists,
                stringLocalizer["DNC lists"],
                stringLocalizer["One row per local do-not-call list: its name, country, import status and progress, and when it was uploaded and imported."]),
            session,
            authorizationService,
            DncRegistryConstants.CollectionName,
            DncRegistryPermissions.ManageDncRegistrySettings)
    {
        var S = stringLocalizer;
        var list = S["List"].Value;
        var import = S["Import"].Value;

        AddField(DncRegistryReportDataSets.ListIdField, S["List ID"], DataFieldType.Text, record => record.ListId, list).Identifier();
        AddField(nameof(LocalDncList.Name), S["Name"], DataFieldType.Text, record => record.Name, list);
        AddField(nameof(LocalDncList.UploadedFileName), S["Uploaded file"], DataFieldType.Text, record => record.UploadedFileName, list);
        AddField(nameof(LocalDncList.CountryCode), S["Country"], DataFieldType.Text, record => record.CountryCode, list);
        AddField(nameof(LocalDncList.PhoneNumberCount), S["Phone numbers"], DataFieldType.Integer, record => record.PhoneNumberCount, list);
        AddField(CreatedUtcField, S["Uploaded"], DataFieldType.DateTime, record => record.CreatedUtc, list);

        AddField(nameof(LocalDncList.Status), S["Status"], DataFieldType.Text, record => record.Status, import)
            .Description = S["Pending, Processing, Completed, Failed, Paused or Deleting."].Value;
        AddField(nameof(LocalDncList.TotalRecords), S["Rows in the file"], DataFieldType.Integer, record => record.TotalRecords, import);
        AddField(nameof(LocalDncList.TotalProcessed), S["Rows processed"], DataFieldType.Integer, record => record.TotalProcessed, import);
        AddField(nameof(LocalDncList.ImportedCount), S["Rows imported"], DataFieldType.Integer, record => record.ImportedCount, import);
        AddField("ErrorCount", S["Rows with errors"], DataFieldType.Integer, record => record.ErrorMessages?.Count ?? 0, import);
        AddField(nameof(LocalDncList.FailedAttempts), S["Failed import attempts"], DataFieldType.Integer, record => record.FailedAttempts, import)
            .Description = S["Import attempts that failed in a row. A successful import resets it."].Value;
        AddField(nameof(LocalDncList.Error), S["Import error"], DataFieldType.Text, record => record.Error, import);
        AddField(nameof(LocalDncList.ProcessSaveUtc), S["Last processed"], DataFieldType.DateTime, record => record.ProcessSaveUtc, import);
        AddField(nameof(LocalDncList.CompletedUtc), S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, import);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<LocalDncListIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<LocalDncListIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<LocalDncListIndex, string>>>(StringComparer.Ordinal)
        {
            [DncRegistryReportDataSets.ListIdField] = index => index.ListId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<LocalDncListIndex> columns)
    {
        columns
            .Enum(nameof(LocalDncList.Status), index => index.Status)
            .Text(nameof(LocalDncList.CountryCode), nameof(LocalDncListIndex.CountryCode));
    }
}
