using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="SurveyInvitation"/> to its <see cref="SurveyInvitationIndex"/> row.
/// </summary>
public sealed class SurveyInvitationIndexProvider : IndexProvider<SurveyInvitation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationIndexProvider"/> class.
    /// </summary>
    public SurveyInvitationIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyInvitation> context)
        => context.For<SurveyInvitationIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new SurveyInvitationIndex
            {
                ItemId = item.ItemId,
                SurveyId = item.SurveyId,
                CollectorId = item.CollectorId,
                Status = item.Status.ToString(),
                UserId = item.UserId,
                RecipientRef = Cut(item.Recipient?.ToString(), 255),
                TokenHash = item.TokenHash,
                SentUtc = item.SentUtc,
                ExpiresUtc = item.ExpiresUtc,
            });

    private static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;
}
