namespace CrestApps.OrchardCore.ContactCenter.Configuration;

/// <summary>
/// Names the Contact Center configuration catalogs as they appear in recipes and deployment plans.
/// </summary>
public static class ContactCenterConfigurationCatalogs
{
    /// <summary>
    /// The identifier of the catalog group that the Contact Center deployment step exports.
    /// </summary>
    public const string Group = "ContactCenter";

    /// <summary>
    /// The recipe step that carries skills.
    /// </summary>
    public const string Skill = "ContactCenterSkill";

    /// <summary>
    /// The recipe step that carries queue groups.
    /// </summary>
    public const string QueueGroup = "ContactCenterQueueGroup";

    /// <summary>
    /// The recipe step that carries business-hours calendars.
    /// </summary>
    public const string BusinessHoursCalendar = "ContactCenterBusinessHoursCalendar";

    /// <summary>
    /// The recipe step that carries queues.
    /// </summary>
    public const string Queue = "ContactCenterQueue";

    /// <summary>
    /// The recipe step that carries entry points.
    /// </summary>
    public const string EntryPoint = "ContactCenterEntryPoint";

    /// <summary>
    /// The recipe step that carries dialer profiles.
    /// </summary>
    public const string DialerProfile = "ContactCenterDialerProfile";

    /// <summary>
    /// The recipe step that carries agent state reason codes.
    /// </summary>
    public const string AgentStateReasonCode = "AgentStateReasonCode";
}
