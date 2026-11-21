using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="QuestionBank"/> to its <see cref="QuestionBankIndex"/> row.
/// </summary>
public sealed class QuestionBankIndexProvider : IndexProvider<QuestionBank>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionBankIndexProvider"/> class.
    /// </summary>
    public QuestionBankIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<QuestionBank> context)
        => context.For<QuestionBankIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new QuestionBankIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Title = Cut(item.Title, 255),
                OwnerRef = Cut(item.Owner?.ToString(), 255),
            });

    private static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;
}
