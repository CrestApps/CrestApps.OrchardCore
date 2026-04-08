using System.ComponentModel.DataAnnotations;
using CrestApps.Core.AI.Mcp.Models;

namespace CrestApps.Core.Mvc.Web.Areas.Mcp.ViewModels;

public sealed class McpConnectionViewModel
{
    public string ItemId { get; set; }

    [Required]
    public string DisplayText { get; set; }

    [Required]
    public string Source { get; set; } = "sse";

    public string Endpoint { get; set; }

    public McpClientAuthenticationType AuthenticationType { get; set; }

    public string ApiKeyHeaderName { get; set; }

    public string ApiKeyPrefix { get; set; }

    public string ApiKey { get; set; }

    public bool HasApiKey { get; set; }

    public string BasicUsername { get; set; }

    public string BasicPassword { get; set; }

    public bool HasBasicPassword { get; set; }

    public string OAuth2TokenEndpoint { get; set; }

    public string OAuth2ClientId { get; set; }

    public string OAuth2ClientSecret { get; set; }

    public bool HasOAuth2ClientSecret { get; set; }

    public string OAuth2Scopes { get; set; }

    public string OAuth2PrivateKey { get; set; }

    public bool HasOAuth2PrivateKey { get; set; }

    public string OAuth2KeyId { get; set; }

    public string OAuth2ClientCertificate { get; set; }

    public bool HasOAuth2ClientCertificate { get; set; }

    public string OAuth2ClientCertificatePassword { get; set; }

    public bool HasOAuth2ClientCertificatePassword { get; set; }

    public string AdditionalHeaders { get; set; }

    public string Command { get; set; }

    public string Arguments { get; set; } = "[]";

    public string WorkingDirectory { get; set; }

    public string EnvironmentVariables { get; set; } = "{}";
}
