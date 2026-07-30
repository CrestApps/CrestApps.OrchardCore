namespace CrestApps.OrchardCore.ContactCenter.Deployments;

/// <summary>
/// Names the recipe steps that carry Contact Center configuration between environments.
/// </summary>
public static class ContactCenterDeploymentSteps
{
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
