using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One row per invitation. The recipient is empty without Export survey responses and for anonymous surveys.
/// </summary>
internal sealed class SurveyInvitationsDataSet : SurveysDataSetBase<SurveyInvitation, SurveyInvitationIndex, SurveyInvitation>
{
    public SurveyInvitationsDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        IStringLocalizer<SurveyInvitationsDataSet> S)
        : base(new DataSetDescriptor("SurveyInvitations", S["Survey invitations"], S["One row per survey invitation."]), session, authorizationService, access, SurveysConstants.CollectionName)
    {
        var group = S["Invitation"].Value;

        AddField("Id", S["Invitation ID"], DataFieldType.Text, invitation => invitation.ItemId, group).IsIdentifier = true;
        AddField("SurveyId", S["Survey ID"], DataFieldType.Text, invitation => invitation.SurveyId, group).IsIdentifier = true;
        AddField("CollectorId", S["Collector ID"], DataFieldType.Text, invitation => invitation.CollectorId, group).IsIdentifier = true;
        AddField("Recipient", S["Recipient"], DataFieldType.Text, invitation => ShowsIdentity(invitation.SurveyId) ? invitation.DisplayName ?? invitation.Email ?? invitation.Recipient?.ToString() : null, group);
        AddField("Status", S["Status"], DataFieldType.Text, invitation => invitation.Status.ToString(), group);
        AddField("SentUtc", S["Sent"], DataFieldType.DateTime, invitation => invitation.SentUtc is { } sent ? DateTime.SpecifyKind(sent, DateTimeKind.Utc) : null, group);
        AddField("Reminders", S["Reminders"], DataFieldType.Integer, invitation => (long)invitation.RemindersSent, group);
        AddField("ResponseId", S["Response ID"], DataFieldType.Text, invitation => ShowsIdentity(invitation.SurveyId) ? invitation.ResponseId : null, group).IsIdentifier = true;
        CompleteDescription();
    }

    protected override IEnumerable<SurveyInvitation> GetRows(SurveyInvitation document)
    {
        yield return document;
    }

    protected override IQueryIndex<SurveyInvitationIndex> OfSurveys(IQueryIndex<SurveyInvitationIndex> rows, string[] surveyIds)
        => rows.Where(index => index.SurveyId.IsIn(surveyIds));
}
