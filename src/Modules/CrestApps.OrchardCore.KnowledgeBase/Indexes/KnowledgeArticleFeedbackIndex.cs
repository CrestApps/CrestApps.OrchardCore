using CrestApps.OrchardCore.KnowledgeBase.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.KnowledgeBase.Indexes;

/// <summary>
/// Finds readers' answers by article, category, day and reader, for the feedback report and personal data requests.
/// </summary>
public sealed class KnowledgeArticleFeedbackIndex : MapIndex
{
    /// <summary>
    /// The longest comment a reader may leave.
    /// </summary>
    public const int MaxCommentLength = 1000;

    public string ItemId { get; set; }

    public string ContentItemId { get; set; }

    public string CategoryId { get; set; }

    public bool Helpful { get; set; }

    public bool HasComment { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string UserId { get; set; }
}

/// <summary>
/// Maps <see cref="KnowledgeArticleFeedback"/> to <see cref="KnowledgeArticleFeedbackIndex"/>, in the Knowledge Base's
/// collection.
/// </summary>
public sealed class KnowledgeArticleFeedbackIndexProvider : IndexProvider<KnowledgeArticleFeedback>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeArticleFeedbackIndexProvider"/> class.
    /// </summary>
    public KnowledgeArticleFeedbackIndexProvider()
    {
        CollectionName = KnowledgeBaseConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<KnowledgeArticleFeedback> context)
        => context.For<KnowledgeArticleFeedbackIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(feedback => new KnowledgeArticleFeedbackIndex
            {
                ItemId = feedback.ItemId,
                ContentItemId = feedback.ContentItemId,
                CategoryId = feedback.CategoryId,
                Helpful = feedback.Helpful,
                HasComment = !string.IsNullOrWhiteSpace(feedback.Comment),
                CreatedUtc = feedback.CreatedUtc,
                UserId = feedback.UserId,
            });
}
