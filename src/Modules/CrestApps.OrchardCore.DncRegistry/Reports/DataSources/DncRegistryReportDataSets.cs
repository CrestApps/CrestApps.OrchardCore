namespace CrestApps.OrchardCore.DncRegistry.Reports.DataSources;

/// <summary>
/// The stable names of the DNC registry report data source, its data sets, and the fields other data sets join on.
/// Report designs store them, so they must never change.
/// </summary>
public static class DncRegistryReportDataSets
{
    /// <summary>
    /// The technical name of the DNC registry data source.
    /// </summary>
    public const string Source = "DncRegistry";

    /// <summary>
    /// The DNC lists data set: one row per local do-not-call list uploaded to the site.
    /// </summary>
    public const string DncLists = "DncLists";

    /// <summary>
    /// The DNC entries data set: one row per phone number imported into a local do-not-call list.
    /// </summary>
    public const string DncEntries = "DncEntries";

    /// <summary>
    /// The name of the field that identifies a local do-not-call list.
    /// </summary>
    public const string ListIdField = "ListId";

    /// <summary>
    /// The name of the field that identifies an entry of a local do-not-call list.
    /// </summary>
    public const string EntryIdField = "EntryId";

    /// <summary>
    /// The name of the field that holds the phone number of an entry, in E.164 format.
    /// </summary>
    public const string PhoneNumberField = "PhoneNumber";
}
