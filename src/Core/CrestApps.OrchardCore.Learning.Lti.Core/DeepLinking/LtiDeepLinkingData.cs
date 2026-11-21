namespace CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;

/// <summary>
/// What a Deep Linking request was for, carried to the tool as the signed <c>data</c> of its settings and required back,
/// unchanged, in its response: the tool, the member of the course staff who asked, the course, and when it stops being
/// valid. Its id makes a response usable once.
/// </summary>
public sealed class LtiDeepLinkingData
{
    /// <summary>
    /// Gets or sets a random id, so one request yields one response.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the id of the external tool.
    /// </summary>
    public string ToolId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the person who asked.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the course the content goes into.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the page the person returns to.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets when the data stops being valid, in UTC.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }
}
