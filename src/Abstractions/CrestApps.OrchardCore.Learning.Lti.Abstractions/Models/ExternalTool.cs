using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Models;

/// <summary>
/// Learning's settings of an LTI 1.3 tool. The tool's client (client id, redirect URIs and, when the tool gives keys
/// rather than a JWKS URL, its key set) is an Orchard Core OpenID application with the same client id; this record holds
/// what LTI adds.
/// </summary>
public sealed class ExternalTool : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the name shown to authors.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets notes for administrators.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the client id of the tool's OpenID application.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the deployment id the platform gives the tool.
    /// </summary>
    public string DeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the tool's OIDC login initiation URL.
    /// </summary>
    public string LoginInitiationUrl { get; set; }

    /// <summary>
    /// Gets or sets the tool's default target link URI (launch URL).
    /// </summary>
    public string TargetLinkUri { get; set; }

    /// <summary>
    /// Gets or sets the tool's Deep Linking URL, or <see langword="null"/> when the tool does not offer Deep Linking.
    /// </summary>
    public string DeepLinkingUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL of the tool's JSON Web Key Set, or <see langword="null"/> when its keys are kept on its
    /// OpenID application.
    /// </summary>
    public string JwksUrl { get; set; }

    /// <summary>
    /// Gets or sets the custom parameters sent with every launch, one <c>name=value</c> per line; values may use the
    /// supported substitution variables.
    /// </summary>
    public string CustomParameters { get; set; }

    /// <summary>
    /// Gets or sets what the tool learns about a person.
    /// </summary>
    public LtiPrivacyLevel PrivacyLevel { get; set; }

    /// <summary>
    /// Gets or sets whether the tool may read the course roster (Names and Role Provisioning Services).
    /// </summary>
    public bool AllowNamesAndRoles { get; set; }

    /// <summary>
    /// Gets or sets whether the tool may manage line items and post scores (Assignment and Grade Services).
    /// </summary>
    public bool AllowGrades { get; set; }

    /// <summary>
    /// Gets or sets whether the tool can be launched.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the random salt of the ids made for the tool, in base64. It moves with the tool, so the ids stay the
    /// same on another site.
    /// </summary>
    public string SubjectSalt { get; set; }

    /// <summary>
    /// Gets or sets when the tool was registered, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the tool last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
