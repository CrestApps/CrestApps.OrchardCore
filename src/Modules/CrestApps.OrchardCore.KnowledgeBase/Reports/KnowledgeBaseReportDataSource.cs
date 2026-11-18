using System.Linq.Expressions;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.KnowledgeBase.Reports;

/// <summary>
/// The Knowledge Base's data for Data Pipelines and the Report Builder: readers' answers to "Was this helpful?".
/// </summary>
internal sealed class KnowledgeBaseReportDataSource : RecordDataSource, IDataSource
{
    /// <summary>
    /// The data source's stable name.
    /// </summary>
    public const string SourceName = "KnowledgeBase";

    private readonly IRecordDataSet[] _dataSets;

    public KnowledgeBaseReportDataSource(KnowledgeArticleFeedbackDataSet feedback)
    {
        _dataSets = [feedback];
    }

    public static string Name => SourceName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<KnowledgeBaseReportDataSource>("Knowledge Base");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<KnowledgeBaseReportDataSource>("Readers' answers to \"Was this helpful?\" on the help articles, with what they said was missing.");

    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// One row per answer to "Was this helpful?": the article, its category, the answer, what was missing, when, and the
/// signed-in reader. Only those allowed to view knowledge base feedback read it.
/// </summary>
internal sealed class KnowledgeArticleFeedbackDataSet : IndexedReportDataSet<KnowledgeArticleFeedback, KnowledgeArticleFeedbackIndex>
{
    /// <summary>
    /// The data set's stable name.
    /// </summary>
    public const string DataSetName = "KnowledgeArticleFeedback";

    public KnowledgeArticleFeedbackDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<KnowledgeArticleFeedbackDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                DataSetName,
                stringLocalizer["Knowledge article feedback"],
                stringLocalizer["One row per answer to \"Was this helpful?\": the article and its category, whether it helped, what the reader said was missing, when, and who answered when they were signed in."]),
            session,
            authorizationService,
            KnowledgeBaseConstants.CollectionName,
            KnowledgeBasePermissions.ViewFeedback)
    {
        var S = stringLocalizer;
        var answer = S["Answer"].Value;

        AddField(nameof(KnowledgeArticleFeedback.ItemId), S["Answer ID"], DataFieldType.Text, record => record.ItemId, answer).Identifier();
        AddField(nameof(KnowledgeArticleFeedback.ContentItemId), S["Article ID"], DataFieldType.Text, record => record.ContentItemId, answer)
            .Identifier(ReportDataReferences.ContentItem(KnowledgeBaseConstants.ArticleType));
        AddField(nameof(KnowledgeArticleFeedback.Title), S["Article"], DataFieldType.Text, record => record.Title, answer).Description = S["The article's title when the reader answered."].Value;
        AddField(nameof(KnowledgeArticleFeedback.CategoryId), S["Category ID"], DataFieldType.Text, record => record.CategoryId, answer).Description = S["The article's category when the reader answered: a term of the Knowledge Base Categories taxonomy."].Value;
        AddField(nameof(KnowledgeArticleFeedback.Helpful), S["Helpful"], DataFieldType.Boolean, record => record.Helpful, answer);
        AddField(nameof(KnowledgeArticleFeedback.Comment), S["What was missing"], DataFieldType.Text, record => record.Comment, answer);
        AddField(nameof(KnowledgeArticleFeedback.CreatedUtc), S["Answered"], DataFieldType.DateTime, record => record.CreatedUtc, answer);
        AddField(nameof(KnowledgeArticleFeedback.UserId), S["Reader"], DataFieldType.Text, record => record.UserId, answer)
            .Identifier(ReportDataReferences.User())
            .Description = S["Empty when a visitor answered."].Value;
        AddField(nameof(KnowledgeArticleFeedback.UserName), S["Reader user name"], DataFieldType.Text, record => record.UserName, answer);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<KnowledgeArticleFeedbackIndex, DateTime>> Column)? DateColumn
        => (nameof(KnowledgeArticleFeedback.CreatedUtc), index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<KnowledgeArticleFeedbackIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<KnowledgeArticleFeedbackIndex, string>>>(StringComparer.Ordinal)
        {
            [nameof(KnowledgeArticleFeedback.ItemId)] = index => index.ItemId,
            [nameof(KnowledgeArticleFeedback.ContentItemId)] = index => index.ContentItemId,
            [nameof(KnowledgeArticleFeedback.CategoryId)] = index => index.CategoryId,
            [nameof(KnowledgeArticleFeedback.UserId)] = index => index.UserId,
        };

    // The comment is on the document only; the index keeps whether there is one.
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
        => ReportIndexAggregator.MapByName<KnowledgeArticleFeedbackIndex>(
        [
            nameof(KnowledgeArticleFeedback.ItemId),
            nameof(KnowledgeArticleFeedback.ContentItemId),
            nameof(KnowledgeArticleFeedback.CategoryId),
            nameof(KnowledgeArticleFeedback.Helpful),
            nameof(KnowledgeArticleFeedback.CreatedUtc),
            nameof(KnowledgeArticleFeedback.UserId),
        ]);
}
