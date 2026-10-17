using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per call recording: which call it is of, who was on it, how long it is, and whether it is stored or
/// erased. Only a principal allowed to listen to anyone's recordings may read it. Where the media is stored, and the
/// provider's own recording handle, are never exposed.
/// </summary>
public sealed class CallRecordingReportDataSet : ContactCenterReportDataSet<CallRecording, CallRecordingIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session recordings are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CallRecordingReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CallRecordingReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                ContactCenterReportDataSets.CallRecordings,
                stringLocalizer["Call recordings"],
                stringLocalizer["One row per call recording: its call, agent, length, and whether it is stored or erased."]),
            session,
            authorizationService,
            ContactCenterPermissions.ListenToAllCallRecordings)
    {
        var S = stringLocalizer;
        var group = S["Recording"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Recording ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField(nameof(CallRecording.Source), S["Recorded by"], DataFieldType.Text, record => record.Source, group);
        AddField(nameof(CallRecording.ProviderName), S["Provider"], DataFieldType.Text, record => record.ProviderName, group);
        AddField(nameof(CallRecording.Format), S["Format"], DataFieldType.Text, record => record.Format, group);
        AddField(nameof(CallRecording.InteractionId), S["Interaction ID"], DataFieldType.Text, record => record.InteractionId, group).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(CallRecording.ActivityItemId), S["Activity ID"], DataFieldType.Text, record => record.ActivityItemId, group).Identifier(ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(CallRecording.AgentUserId), S["Agent user ID"], DataFieldType.Text, record => record.AgentUserId, group).Identifier(ContactCenterReportDataSets.UserReference());
        AddField(nameof(CallRecording.CustomerAddress), S["Customer address"], DataFieldType.Text, record => record.CustomerAddress, group);
        AddField(nameof(CallRecording.Direction), S["Direction"], DataFieldType.Text, record => record.Direction, group);
        AddField(nameof(CallRecording.StartedUtc), S["Started"], DataFieldType.DateTime, record => record.StartedUtc, group);
        AddField(nameof(CallRecording.EndedUtc), S["Ended"], DataFieldType.DateTime, record => record.EndedUtc, group);
        AddField(nameof(CallRecording.DurationSeconds), S["Length (seconds)"], DataFieldType.Decimal, record => record.DurationSeconds, group);
        AddField("IsStored", S["Stored"], DataFieldType.Boolean, record => record.StoredUtc.HasValue, group);
        AddField(nameof(CallRecording.StoredUtc), S["Stored on"], DataFieldType.DateTime, record => record.StoredUtc, group);
        AddField("IsErased", S["Erased"], DataFieldType.Boolean, record => record.ErasedUtc.HasValue, group);
        AddField(nameof(CallRecording.ErasedUtc), S["Erased on"], DataFieldType.DateTime, record => record.ErasedUtc, group);
        AddField(nameof(CallRecording.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CallRecordingIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CallRecordingIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["InteractionId"] = index => index.InteractionId,
            ["ActivityItemId"] = index => index.ActivityItemId,
        };

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CallRecordingIndex, DateTime>> Column)? DateColumn
        => (nameof(CallRecording.StartedUtc), index => index.StartedUtc);
}
