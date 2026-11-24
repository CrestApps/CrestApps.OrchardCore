namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// What a launch is for, carried through the tool's login request as the signed <c>lti_message_hint</c>: the message type,
/// the tool, the person who started it, the course and the course item, and when it stops being valid.
/// </summary>
public sealed class LtiMessageHint
{
    /// <summary>
    /// Gets or sets the message type: <see cref="LtiConstants.MessageTypes.ResourceLinkRequest"/> or
    /// <see cref="LtiConstants.MessageTypes.DeepLinkingRequest"/>.
    /// </summary>
    public string MessageType { get; set; }

    /// <summary>
    /// Gets or sets the id of the external tool.
    /// </summary>
    public string ToolId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the person who started the launch.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the External tool course item, for a resource link launch.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the learner's enrollment, for a learner's launch.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the LIS context roles, worked out when the launch started.
    /// </summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the page the person returns to.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets when the hint stops being valid, in UTC.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }
}
