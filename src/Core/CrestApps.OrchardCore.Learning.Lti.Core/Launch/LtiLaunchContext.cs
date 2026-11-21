namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Everything a launch message says, gathered from the message hint: the person, their roles, the course, the course
/// item and the services the tool may use. Names and the e-mail address are filled only when the tool's privacy level
/// allows them.
/// </summary>
public sealed class LtiLaunchContext
{
    /// <summary>
    /// Gets or sets the message type.
    /// </summary>
    public string MessageType { get; set; }

    /// <summary>
    /// Gets or sets the user id. It never goes into a message.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the id the tool knows the person by.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the full name, when the privacy level allows it.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the given name, when the privacy level allows it.
    /// </summary>
    public string GivenName { get; set; }

    /// <summary>
    /// Gets or sets the family name, when the privacy level allows it.
    /// </summary>
    public string FamilyName { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address, when the privacy level allows it.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the LIS context roles.
    /// </summary>
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the content item id of the course (the context id).
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the course's title.
    /// </summary>
    public string CourseTitle { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the External tool course item (the resource link id), for a resource link launch.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the course item's title.
    /// </summary>
    public string ItemTitle { get; set; }

    /// <summary>
    /// Gets or sets the target link URI.
    /// </summary>
    public string TargetLinkUri { get; set; }

    /// <summary>
    /// Gets or sets the custom parameters of the course item.
    /// </summary>
    public string LinkCustomParameters { get; set; }

    /// <summary>
    /// Gets or sets the URL of the course item's line item, when the tool may post grades for it.
    /// </summary>
    public string LineItemUrl { get; set; }

    /// <summary>
    /// Gets or sets the page the person returns to.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the person's locale, such as <c>en-US</c>.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Gets or sets the signed data a Deep Linking request carries and its response must echo.
    /// </summary>
    public string DeepLinkingData { get; set; }
}
