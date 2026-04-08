using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CrestApps.Core.AI.A2A.Models;

namespace CrestApps.Core.Mvc.Web.Areas.A2A.ViewModels;

public sealed class A2AConnectionViewModel
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    public string ItemId { get; set; }

    [Required]
    public string DisplayText { get; set; }

    [Required]
    public string Endpoint { get; set; }

    public A2AClientAuthenticationType AuthenticationType { get; set; }

    public string ApiKeyHeaderName { get; set; }

    public string ApiKeyPrefix { get; set; }

    public string ApiKey { get; set; }

    public string BasicUsername { get; set; }

    public string BasicPassword { get; set; }

    public string OAuth2TokenEndpoint { get; set; }

    public string OAuth2ClientId { get; set; }

    public string OAuth2ClientSecret { get; set; }

    public string OAuth2Scopes { get; set; }

    public string OAuth2PrivateKey { get; set; }

    public string OAuth2KeyId { get; set; }

    public string OAuth2ClientCertificate { get; set; }

    public string OAuth2ClientCertificatePassword { get; set; }

    public string AdditionalHeaders { get; set; }

    public bool HasApiKey { get; set; }

    public bool HasBasicPassword { get; set; }

    public bool HasOAuth2ClientSecret { get; set; }

    public bool HasOAuth2PrivateKey { get; set; }

    public bool HasOAuth2ClientCertificate { get; set; }

    public bool HasOAuth2ClientCertificatePassword { get; set; }

    public static A2AConnectionViewModel FromConnection(A2AConnection connection)
    {
        var metadata = connection.As<A2AConnectionMetadata>();

        return new A2AConnectionViewModel
        {
            ItemId = connection.ItemId,
            DisplayText = connection.DisplayText,
            Endpoint = connection.Endpoint,
            AuthenticationType = metadata.AuthenticationType == A2AClientAuthenticationType.Anonymous && metadata.AdditionalHeaders is { Count: > 0 }
                ? A2AClientAuthenticationType.CustomHeaders
                : metadata.AuthenticationType,
            ApiKeyHeaderName = metadata.ApiKeyHeaderName,
            ApiKeyPrefix = metadata.ApiKeyPrefix,
            BasicUsername = metadata.BasicUsername,
            OAuth2TokenEndpoint = metadata.OAuth2TokenEndpoint,
            OAuth2ClientId = metadata.OAuth2ClientId,
            OAuth2Scopes = metadata.OAuth2Scopes,
            OAuth2KeyId = metadata.OAuth2KeyId,
            AdditionalHeaders = metadata.AdditionalHeaders is null
                ? null
                : JsonSerializer.Serialize(metadata.AdditionalHeaders, _serializerOptions),
            HasApiKey = !string.IsNullOrEmpty(metadata.ApiKey),
            HasBasicPassword = !string.IsNullOrEmpty(metadata.BasicPassword),
            HasOAuth2ClientSecret = !string.IsNullOrEmpty(metadata.OAuth2ClientSecret),
            HasOAuth2PrivateKey = !string.IsNullOrEmpty(metadata.OAuth2PrivateKey),
            HasOAuth2ClientCertificate = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificate),
            HasOAuth2ClientCertificatePassword = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificatePassword),
        };
    }
}
