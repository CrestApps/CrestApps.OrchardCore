namespace CrestApps.OrchardCore.Learning.Credentials.Models;

/// <summary>
/// One rule of a credential definition: a kind (an open string such as <c>CourseCompleted</c>) and its target.
/// </summary>
public sealed class CredentialAwardRule
{
    /// <summary>
    /// Gets or sets the kind of the rule.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the id of what the rule watches, such as a course's content item id.
    /// </summary>
    public string TargetId { get; set; }
}
