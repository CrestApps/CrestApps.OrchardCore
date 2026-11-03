using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Options;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// Maps <see cref="AIConversationEvaluation"/> documents to <see cref="AIConversationEvaluationIndex"/> rows in the
/// AI collection.
/// </summary>
public sealed class AIConversationEvaluationIndexProvider : IndexProvider<AIConversationEvaluation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationEvaluationIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options that name the AI collection.</param>
    public AIConversationEvaluationIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIConversationEvaluation> context)
    {
        context.For<AIConversationEvaluationIndex>()
            .Map(evaluation => new AIConversationEvaluationIndex
            {
                ItemId = evaluation.ItemId,
                SessionId = evaluation.SessionId,
                ProfileId = evaluation.ProfileId,
                Label = evaluation.Label is { Length: > AIConversationEvaluationIndexSchema.LabelLength }
                    ? evaluation.Label[..AIConversationEvaluationIndexSchema.LabelLength]
                    : evaluation.Label,
                Score = evaluation.Score,
                Passed = evaluation.Passed,
                Resolved = evaluation.Resolved,
                IssueCount = evaluation.Issues?.Count ?? 0,
                IssueKinds = evaluation.Issues is { Count: > 0 }
                    ? string.Join(',', evaluation.Issues.Select(issue => issue.Kind).Where(kind => !string.IsNullOrEmpty(kind)).Distinct(StringComparer.Ordinal))
                    : null,
                EvaluatedUtc = evaluation.EvaluatedUtc,
            });
    }
}
