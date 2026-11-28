using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="BankQuestion"/> to its <see cref="BankQuestionIndex"/> row.
/// </summary>
public sealed class BankQuestionIndexProvider : IndexProvider<BankQuestion>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BankQuestionIndexProvider"/> class.
    /// </summary>
    public BankQuestionIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<BankQuestion> context)
        => context.For<BankQuestionIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new BankQuestionIndex
            {
                ItemId = item.ItemId,
                BankId = item.BankId,
                ControlType = item.Question?.ControlType,
                Title = Cut(item.Question?.Title, 255),
                Difficulty = item.Difficulty,
                Retired = item.Retired,
                SourceIdentifier = Cut(item.SourceIdentifier, 255),
                ModifiedUtc = item.ModifiedUtc,
            });

    private static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;
}
