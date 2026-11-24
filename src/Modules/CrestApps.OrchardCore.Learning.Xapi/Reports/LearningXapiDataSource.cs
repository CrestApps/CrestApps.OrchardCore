using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Xapi.Reports;

/// <summary>
/// The Learning statements data source of Data Pipelines and the Report Builder.
/// </summary>
public sealed class LearningXapiDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningXapiDataSource"/> class.
    /// </summary>
    /// <param name="statements">The statements data set.</param>
    public LearningXapiDataSource(LearningStatementsReportDataSet statements)
    {
        _dataSets = [statements];
    }

    /// <summary>Gets the name.</summary>
    public static string Name => XapiConstants.DataSourceName;

    /// <summary>Gets the display name.</summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningXapiDataSource>("Learning statements");

    /// <summary>Gets the description.</summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningXapiDataSource>("The xAPI statements of the Learning Record Store: who did what, with which result, and when it was stored.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// One row per statement, voided ones included and marked. People are an account name (the user id when a launched
/// course of this site wrote the statement) or the SHA-1 hash of a mailbox, never an e-mail address; an agent object is
/// named only by its name.
/// </summary>
public sealed class LearningStatementsReportDataSet : IndexedReportDataSet<XapiStatementRecord, XapiStatementIndex, LearningStatementRow>
{
    /// <summary>
    /// The name of the data set.
    /// </summary>
    public const string DataSetName = "LearningStatements";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningStatementsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningStatementsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningStatementsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Learning statements"], stringLocalizer["One row per xAPI statement of the Learning Record Store, with its verb, object, result and times."]), session, authorizationService, XapiConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Statement"].Value;

        AddField("StatementId", S["Statement ID"], DataFieldType.Text, row => row.Record.StatementId, group).Identifier();
        AddField("Verb", S["Verb ID"], DataFieldType.Text, row => row.Record.Verb, group);
        AddField("VerbLabel", S["Verb"], DataFieldType.Text, row => row.Summary.Verb, group);
        AddField("ObjectType", S["Object type"], DataFieldType.Text, row => row.Record.ObjectType, group);
        AddField("ObjectId", S["Object ID"], DataFieldType.Text, row => row.Record.ObjectType is "Activity" or "StatementRef" ? row.Record.ObjectId : null, group);
        AddField("ObjectLabel", S["Object"], DataFieldType.Text, row => row.ObjectLabel, group);
        AddField("ActorAccountName", S["Actor account"], DataFieldType.Text, row => row.Record.ActorAccountName, group).Identifier(ReportDataReferences.User());
        AddField("ActorAccountHomePage", S["Actor account home page"], DataFieldType.Text, row => row.Record.ActorAccountHomePage, group);
        AddField("ActorMailboxHash", S["Actor mailbox (SHA-1)"], DataFieldType.Text, row => row.ActorMailboxHash, group);
        AddField("Registration", S["Registration"], DataFieldType.Text, row => row.Record.Registration, group).Identifier();
        AddField("Success", S["Success"], DataFieldType.Boolean, row => row.Summary.Success, group);
        AddField("Completion", S["Completion"], DataFieldType.Boolean, row => row.Summary.Completion, group);
        AddField("ScoreScaled", S["Scaled score"], DataFieldType.Decimal, row => row.Summary.ScoreScaled, group);
        AddField("ScoreRaw", S["Raw score"], DataFieldType.Decimal, row => row.Summary.ScoreRaw, group);
        AddField("Duration", S["Duration"], DataFieldType.Text, row => row.Summary.Duration, group);
        AddField("TimestampUtc", S["Timestamp"], DataFieldType.DateTime, row => row.Record.TimestampUtc, group);
        AddField("StoredUtc", S["Stored"], DataFieldType.DateTime, row => row.Record.StoredUtc, group);
        AddField("Voided", S["Voided"], DataFieldType.Boolean, row => row.Record.Voided, group);
        AddField("ClientId", S["Stored by client"], DataFieldType.Text, row => row.Record.ClientId, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<XapiStatementIndex, DateTime>> Column)? DateColumn
        => ("StoredUtc", index => index.StoredUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<XapiStatementIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<XapiStatementIndex, string>>>(StringComparer.Ordinal)
        {
            ["StatementId"] = index => index.StatementId,
            ["Registration"] = index => index.Registration,
            ["ActorAccountName"] = index => index.ActorAccountName,
        };

    /// <inheritdoc />
    protected override IEnumerable<LearningStatementRow> GetRows(XapiStatementRecord document)
    {
        var statement = JsonNode.Parse(document.Statement).AsObject();
        var summary = XapiStatementSummary.Of(statement);
        var actor = statement["actor"] as JsonObject;
        var value = statement["object"] as JsonObject;
        var mbox = XapiAgents.Text(actor?["mbox"]);

#pragma warning disable CA5350 // xAPI's mbox_sha1sum is the SHA-1 hash of the mailbox by definition.
        yield return new LearningStatementRow
        {
            Record = document,
            Summary = summary,

            // An agent object is named by its name only: its identifier may be an e-mail address.
            ObjectLabel = document.ObjectType is "Agent" or "Group" ? XapiAgents.Text(value?["name"]) : summary.Object,
            ActorMailboxHash = mbox is not null
                ? Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(mbox)))
                : XapiAgents.Text(actor?["mbox_sha1sum"])?.ToLowerInvariant(),
        };
#pragma warning restore CA5350
    }
}

/// <summary>
/// A row of the Learning statements data set.
/// </summary>
public sealed class LearningStatementRow
{
    /// <summary>Gets the record.</summary>
    public XapiStatementRecord Record { get; init; }

    /// <summary>Gets the summary of the statement.</summary>
    public XapiStatementSummary Summary { get; init; }

    /// <summary>Gets the label of the object, never an e-mail address.</summary>
    public string ObjectLabel { get; init; }

    /// <summary>Gets the SHA-1 hash of the actor's mailbox, as xAPI's mbox_sha1sum.</summary>
    public string ActorMailboxHash { get; init; }
}
