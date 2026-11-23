using CrestApps.Core;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
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
                SealLookup = item.SealLookup,
                Email = Cut(item.Email?.Trim().ToLowerInvariant(), 254),
                LastReminderUtc = item.LastReminderUtc,
                SentUtc = item.SentUtc,
                ExpiresUtc = item.ExpiresUtc,
                Held = IsHeld(item),
                RemindersSent = item.RemindersSent,
                RemindersStopped = SurveyInvitationReminderFailures.HaveStopped(item),
            });

    private static bool IsHeld(SurveyInvitation item)
        => item.Status == SurveyInvitationStatus.Pending && item.TryGet<SurveyInvitationHold>(out var hold) && hold is not null;

    private static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;
}
