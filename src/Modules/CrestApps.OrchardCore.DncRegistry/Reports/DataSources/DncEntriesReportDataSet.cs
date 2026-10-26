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
/// The <c>DncEntries</c> data set: one row per phone number imported into a local do-not-call list, with its list and
/// country, so a report can count the numbers of each list or join the numbers to the phone numbers of other records.
/// Only principals allowed to manage the DNC registry settings, who upload the lists, read it.
/// </summary>
/// <remarks>
/// An entry has no date, so the data set has no main date and is read newest first by its index rows.
/// </remarks>
public sealed class DncEntriesReportDataSet : IndexedReportDataSet<LocalDncEntry, LocalDncEntryIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DncEntriesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the entries are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DncEntriesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<DncEntriesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                DncRegistryReportDataSets.DncEntries,
                stringLocalizer["DNC entries"],
                stringLocalizer["One row per phone number imported into a local do-not-call list, with its list and country."]),
            session,
            authorizationService,
            DncRegistryConstants.CollectionName,
            DncRegistryPermissions.ManageDncRegistrySettings)
    {
        var S = stringLocalizer;
        var entry = S["Entry"].Value;

        AddField(DncRegistryReportDataSets.EntryIdField, S["Entry ID"], DataFieldType.Text, record => record.EntryId, entry).Identifier();
        AddField(DncRegistryReportDataSets.ListIdField, S["List ID"], DataFieldType.Text, record => record.ListId, entry)
            .Identifier(new DataFieldReference(DncRegistryReportDataSets.Source, DncRegistryReportDataSets.DncLists, DncRegistryReportDataSets.ListIdField));
        AddField(nameof(LocalDncEntry.CountryCode), S["Country"], DataFieldType.Text, record => record.CountryCode, entry);
        AddField(DncRegistryReportDataSets.PhoneNumberField, S["Phone number"], DataFieldType.Text, record => record.PhoneNumber, entry)
            .Description = S["The phone number in E.164 format, such as +14255551212."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<LocalDncEntryIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<LocalDncEntryIndex, string>>>(StringComparer.Ordinal)
        {
            [DncRegistryReportDataSets.EntryIdField] = index => index.EntryId,
            [DncRegistryReportDataSets.ListIdField] = index => index.ListId,
            [DncRegistryReportDataSets.PhoneNumberField] = index => index.PhoneNumber,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<LocalDncEntryIndex> columns)
    {
        columns.Text(nameof(LocalDncEntry.CountryCode), nameof(LocalDncEntryIndex.CountryCode));
    }
}
