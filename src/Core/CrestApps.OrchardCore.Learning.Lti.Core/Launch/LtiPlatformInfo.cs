namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// What a tool is told about the platform: its issuer, its instance and the endpoints of the launch and the services.
/// </summary>
public sealed class LtiPlatformInfo
{
    /// <summary>
    /// Gets or sets the issuer: the site's base URL, without a trailing slash.
    /// </summary>
    public string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the stable id of this platform instance (the tenant).
    /// </summary>
    public string Guid { get; set; }

    /// <summary>
    /// Gets or sets the site's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the authorization endpoint of the third-party initiated login.
    /// </summary>
    public string AuthorizationEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the OpenID server's token endpoint, for the services.
    /// </summary>
    public string TokenEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the URL of the platform's JSON Web Key Set.
    /// </summary>
    public string JwksUrl { get; set; }

    /// <summary>
    /// Gets or sets where a tool posts its Deep Linking response.
    /// </summary>
    public string DeepLinkingReturnUrl { get; set; }

    /// <summary>
    /// Gets the line items service of a course.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <returns>The URL.</returns>
    public string LineItemsUrl(string courseId)
        => $"{Issuer}/learning/lti/ags/{Uri.EscapeDataString(courseId)}/lineitems";

    /// <summary>
    /// Gets a line item of a course.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="lineItemId">The line item.</param>
    /// <returns>The URL.</returns>
    public string LineItemUrl(string courseId, string lineItemId)
        => $"{LineItemsUrl(courseId)}/{Uri.EscapeDataString(lineItemId)}";

    /// <summary>
    /// Gets the memberships service of a course.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <returns>The URL.</returns>
    public string MembershipsUrl(string courseId)
        => $"{Issuer}/learning/lti/nrps/{Uri.EscapeDataString(courseId)}/memberships";
}
