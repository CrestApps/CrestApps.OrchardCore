using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per message left in a queue's shared voicemail box, and how it was worked. The principal reads only the
/// boxes of the queues it may answer shared voicemail for, exactly as the shared voicemail list shows them.
/// </summary>
public sealed class SharedVoicemailReportDataSet : ContactCenterReportDataSet<SharedVoicemail, SharedVoicemailIndex>
{
    private readonly ISharedVoicemailAuthorizationService _sharedVoicemailAuthorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedVoicemailReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session voicemails are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="sharedVoicemailAuthorizationService">Decides which queues' voicemail the principal may read.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SharedVoicemailReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ISharedVoicemailAuthorizationService sharedVoicemailAuthorizationService,
        IStringLocalizer<SharedVoicemailReportDataSet> stringLocalizer)
        : base(
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.SharedVoicemails,
                stringLocalizer["Shared voicemails"],
                stringLocalizer["One row per message left in a queue's shared voicemail box, and how it was worked."]),
            session,
            authorizationService,
            ContactCenterPermissions.AccessSharedVoicemail)
    {
        _sharedVoicemailAuthorizationService = sharedVoicemailAuthorizationService;

        var S = stringLocalizer;
        var message = S["Message"].Value;
        var handling = S["Handling"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Voicemail ID"], ReportDataType.Text, record => record.ItemId, message, isIdentifier: true);
        AddField(nameof(SharedVoicemail.InteractionId), S["Interaction ID"], ReportDataType.Text, record => record.InteractionId, message, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(SharedVoicemail.QueueId), S["Queue ID"], ReportDataType.Text, record => record.QueueId, message, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(SharedVoicemail.CallerNumber), S["Caller number"], ReportDataType.Text, record => record.CallerNumber, message);
        AddField(nameof(SharedVoicemail.CallerName), S["Caller name"], ReportDataType.Text, record => record.CallerName, message);
        AddField(nameof(SharedVoicemail.ContactContentItemId), S["Contact ID"], ReportDataType.Text, record => record.ContactContentItemId, message, isIdentifier: true);
        AddField(nameof(SharedVoicemail.ContactContentType), S["Contact type"], ReportDataType.Text, record => record.ContactContentType, message);
        AddField(nameof(SharedVoicemail.ReceivedUtc), S["Received"], ReportDataType.DateTime, record => record.ReceivedUtc, message);
        AddField(nameof(SharedVoicemail.Status), S["Status"], ReportDataType.Text, record => record.Status, message);

        AddField(nameof(SharedVoicemail.ClaimedByUserId), S["Claimed by user ID"], ReportDataType.Text, record => record.ClaimedByUserId, handling, isIdentifier: true, ContactCenterReportDataSets.UserReference());
        AddField(nameof(SharedVoicemail.ClaimedByUserName), S["Claimed by"], ReportDataType.Text, record => record.ClaimedByUserName, handling);
        AddField(nameof(SharedVoicemail.ClaimedUtc), S["Claimed"], ReportDataType.DateTime, record => record.ClaimedUtc, handling);
        AddField(nameof(SharedVoicemail.ResolvedByUserId), S["Resolved by user ID"], ReportDataType.Text, record => record.ResolvedByUserId, handling, isIdentifier: true, ContactCenterReportDataSets.UserReference());
        AddField(nameof(SharedVoicemail.ResolvedByUserName), S["Resolved by"], ReportDataType.Text, record => record.ResolvedByUserName, handling);
        AddField(nameof(SharedVoicemail.ResolvedUtc), S["Resolved"], ReportDataType.DateTime, record => record.ResolvedUtc, handling);
        AddField(nameof(SharedVoicemail.ResolutionNote), S["Resolution note"], ReportDataType.Text, record => record.ResolutionNote, handling);
        AddField(nameof(SharedVoicemail.CallbackRequestId), S["Callback ID"], ReportDataType.Text, record => record.CallbackRequestId, handling, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.CallbackRequests));
        AddField(nameof(SharedVoicemail.CallbackRequestedUtc), S["Callback requested"], ReportDataType.DateTime, record => record.CallbackRequestedUtc, handling);
        AddField("ResponseSeconds", S["Time to resolve (seconds)"], ReportDataType.Decimal, record => SecondsBetween(record.ReceivedUtc, record.ResolvedUtc), handling);
        AddField(nameof(SharedVoicemail.CreatedUtc), S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, handling);
        AddField(nameof(SharedVoicemail.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, handling);
    }

    /// <inheritdoc/>
    // Only the queues the principal may answer are read, which the grouping statement cannot apply.
    /// <inheritdoc/>
    protected override bool CanAggregate => false;

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<SharedVoicemailIndex, DateTime>> Column)? DateColumn
        => (nameof(SharedVoicemail.ReceivedUtc), index => index.ReceivedUtc);

    /// <inheritdoc/>
    protected override async Task<IQuery<SharedVoicemail, SharedVoicemailIndex>> RestrictAsync(
        IQuery<SharedVoicemail, SharedVoicemailIndex> records,
        ReportDataSourceContext context,
        CancellationToken cancellationToken)
    {
        var access = await _sharedVoicemailAuthorizationService.GetAccessAsync(context?.User, cancellationToken);

        if (access is null || !access.CanAccess)
        {
            return null;
        }

        if (access.AllQueues)
        {
            return records;
        }

        var queueIds = access.QueueIds?.Where(queueId => !string.IsNullOrEmpty(queueId)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        return queueIds.Length == 0 ? null : records.Where(index => index.QueueId.IsIn(queueIds));
    }
}
