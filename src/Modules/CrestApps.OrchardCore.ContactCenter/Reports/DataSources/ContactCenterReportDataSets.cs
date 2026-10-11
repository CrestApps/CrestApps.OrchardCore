using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources;

/// <summary>
/// The stable names of the Contact Center report data source and its data sets. Report designs store them, so they
/// must never change.
/// </summary>
public static class ContactCenterReportDataSets
{
    /// <summary>
    /// The technical name of the Contact Center data source.
    /// </summary>
    public const string Source = "ContactCenter";

    /// <summary>
    /// The name of the identifier field every Contact Center data set has.
    /// </summary>
    public const string ItemIdField = "ItemId";

    /// <summary>
    /// The interactions data set.
    /// </summary>
    public const string Interactions = "Interactions";

    /// <summary>
    /// The interaction events data set.
    /// </summary>
    public const string InteractionEvents = "InteractionEvents";

    /// <summary>
    /// The call sessions data set.
    /// </summary>
    public const string CallSessions = "CallSessions";

    /// <summary>
    /// The call quality data set.
    /// </summary>
    public const string CallQuality = "CallQuality";

    /// <summary>
    /// The call recordings data set.
    /// </summary>
    public const string CallRecordings = "CallRecordings";

    /// <summary>
    /// The callback requests data set.
    /// </summary>
    public const string CallbackRequests = "CallbackRequests";

    /// <summary>
    /// The dialer profiles data set.
    /// </summary>
    public const string DialerProfiles = "DialerProfiles";

    /// <summary>
    /// The queues data set.
    /// </summary>
    public const string Queues = "Queues";

    /// <summary>
    /// The queue groups data set.
    /// </summary>
    public const string QueueGroups = "QueueGroups";

    /// <summary>
    /// The queue items data set.
    /// </summary>
    public const string QueueItems = "QueueItems";

    /// <summary>
    /// The agent profiles data set.
    /// </summary>
    public const string AgentProfiles = "AgentProfiles";

    /// <summary>
    /// The agent sessions data set.
    /// </summary>
    public const string AgentSessions = "AgentSessions";

    /// <summary>
    /// The shared voicemails data set.
    /// </summary>
    public const string SharedVoicemails = "SharedVoicemails";

    /// <summary>
    /// The technical name of the Omnichannel data source.
    /// </summary>
    public const string OmnichannelSource = "Omnichannel";

    /// <summary>
    /// The technical name of the Omnichannel activities data set.
    /// </summary>
    public const string OmnichannelActivities = "Activities";

    /// <summary>
    /// Creates a reference to the identifier of a Contact Center data set.
    /// </summary>
    /// <param name="dataSet">The technical name of the referenced data set.</param>
    /// <returns>The reference.</returns>
    public static ReportFieldReference Reference(string dataSet)
        => new(Source, dataSet, ItemIdField);

    /// <summary>
    /// Creates a reference to the identifier of an Omnichannel activity.
    /// </summary>
    /// <returns>The reference.</returns>
    public static ReportFieldReference ActivityReference()
        => new(OmnichannelSource, OmnichannelActivities, ItemIdField);

    /// <summary>
    /// Creates a reference to the identifier of a user.
    /// </summary>
    /// <returns>The reference.</returns>
    public static ReportFieldReference UserReference()
        => new(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField);
}
