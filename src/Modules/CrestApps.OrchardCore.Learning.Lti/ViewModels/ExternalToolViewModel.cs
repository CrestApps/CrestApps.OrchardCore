using CrestApps.OrchardCore.Learning.Lti.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Lti.ViewModels;

/// <summary>
/// The editor of an external tool: Learning's settings, the OpenID client's redirect URIs and key set, and the platform's
/// details to give the tool.
/// </summary>
public class ExternalToolViewModel
{
    public string Name { get; set; }

    public string Description { get; set; }

    public string ClientId { get; set; }

    public string DeploymentId { get; set; }

    public string LoginInitiationUrl { get; set; }

    public string TargetLinkUri { get; set; }

    public string DeepLinkingUrl { get; set; }

    public string RedirectUris { get; set; }

    public string JwksUrl { get; set; }

    public string KeySet { get; set; }

    public string CustomParameters { get; set; }

    public LtiPrivacyLevel PrivacyLevel { get; set; }

    public bool AllowNamesAndRoles { get; set; }

    public bool AllowGrades { get; set; }

    public bool Enabled { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public string Issuer { get; set; }

    [BindNever]
    public string AuthorizationEndpoint { get; set; }

    [BindNever]
    public string TokenEndpoint { get; set; }

    [BindNever]
    public string PlatformJwksUrl { get; set; }

    [BindNever]
    public string DeepLinkingReturnUrl { get; set; }

    [BindNever]
    public IReadOnlyList<string> SupportedVariables { get; set; } = [];
}
