using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="BankQuestion"/> to one <see cref="BankQuestionTagIndex"/> row per tag.
/// </summary>
public sealed class BankQuestionTagIndexProvider : IndexProvider<BankQuestion>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BankQuestionTagIndexProvider"/> class.
    /// </summary>
    public BankQuestionTagIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<BankQuestion> context)
        => context.For<BankQuestionTagIndex>()
            .ExclusiveToDocumentType()
            .Map(item => (item.Tags ?? [])
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(tag => new BankQuestionTagIndex
                {
                    BankQuestionId = item.ItemId,
                    BankId = item.BankId,
                    Tag = tag.Length > 100 ? tag.Substring(0, 100) : tag,
                    Retired = item.Retired,
                }));
}
