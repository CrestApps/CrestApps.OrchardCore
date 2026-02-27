using System.Text;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

internal sealed class SseClientTransportProvider : IMcpClientTransportProvider
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOAuth2TokenService _oauth2TokenService;
    private readonly ILogger _logger;

    public SseClientTransportProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOAuth2TokenService oauth2TokenService,
        ILogger<SseClientTransportProvider> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _oauth2TokenService = oauth2TokenService;
        _logger = logger;
    }

    public bool CanHandle(McpConnection connection)
        => connection.Source == McpConstants.TransportTypes.Sse;

    public async Task<IClientTransport> GetAsync(McpConnection connection)
    {
        var metadata = connection.As<SseMcpConnectionMetadata>();

        var headers = await BuildHeadersAsync(metadata);

        return new HttpClientTransport(new HttpClientTransportOptions()
        {
            Endpoint = metadata.Endpoint,
            AdditionalHeaders = headers,
        });
    }

    private async Task<Dictionary<string, string>> BuildHeadersAsync(SseMcpConnectionMetadata metadata)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var protector = _dataProtectionProvider.CreateProtector(McpConstants.DataProtectionPurpose);

        switch (metadata.AuthenticationType)
        {
            case McpClientAuthenticationType.ApiKey:
                if (!string.IsNullOrEmpty(metadata.ApiKey))
                {
                    var apiKey = Unprotect(protector, metadata.ApiKey);
                    var headerName = string.IsNullOrWhiteSpace(metadata.ApiKeyHeaderName)
                        ? "Authorization"
                        : metadata.ApiKeyHeaderName;
                    var value = !string.IsNullOrWhiteSpace(metadata.ApiKeyPrefix)
                        ? $"{metadata.ApiKeyPrefix} {apiKey}"
                        : apiKey;

                    headers[headerName] = value;
                }
                break;

            case McpClientAuthenticationType.Basic:
                if (!string.IsNullOrEmpty(metadata.BasicUsername))
                {
                    var password = !string.IsNullOrEmpty(metadata.BasicPassword)
                        ? Unprotect(protector, metadata.BasicPassword)
                        : string.Empty;
                    var credentials = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes($"{metadata.BasicUsername}:{password}"));

                    headers["Authorization"] = $"Basic {credentials}";
                }
                break;

            case McpClientAuthenticationType.OAuth2ClientCredentials:
                if (!string.IsNullOrEmpty(metadata.OAuth2TokenEndpoint) &&
                    !string.IsNullOrEmpty(metadata.OAuth2ClientId) &&
                    !string.IsNullOrEmpty(metadata.OAuth2ClientSecret))
                {
                    var clientSecret = Unprotect(protector, metadata.OAuth2ClientSecret);

                    try
                    {
                        var token = await _oauth2TokenService.AcquireTokenAsync(
                            metadata.OAuth2TokenEndpoint,
                            metadata.OAuth2ClientId,
                            clientSecret,
                            metadata.OAuth2Scopes);

                        headers["Authorization"] = $"Bearer {token}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to acquire OAuth2 token from '{TokenEndpoint}'.", metadata.OAuth2TokenEndpoint);
                        throw;
                    }
                }
                break;

            case McpClientAuthenticationType.OAuth2PrivateKeyJwt:
                if (!string.IsNullOrEmpty(metadata.OAuth2TokenEndpoint) &&
                    !string.IsNullOrEmpty(metadata.OAuth2ClientId) &&
                    !string.IsNullOrEmpty(metadata.OAuth2PrivateKey))
                {
                    var privateKey = Unprotect(protector, metadata.OAuth2PrivateKey);

                    try
                    {
                        var token = await _oauth2TokenService.AcquireTokenWithPrivateKeyJwtAsync(
                            metadata.OAuth2TokenEndpoint,
                            metadata.OAuth2ClientId,
                            privateKey,
                            metadata.OAuth2KeyId,
                            metadata.OAuth2Scopes);

                        headers["Authorization"] = $"Bearer {token}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to acquire OAuth2 token via Private Key JWT from '{TokenEndpoint}'.", metadata.OAuth2TokenEndpoint);
                        throw;
                    }
                }
                break;

            case McpClientAuthenticationType.OAuth2Mtls:
                if (!string.IsNullOrEmpty(metadata.OAuth2TokenEndpoint) &&
                    !string.IsNullOrEmpty(metadata.OAuth2ClientId) &&
                    !string.IsNullOrEmpty(metadata.OAuth2ClientCertificate))
                {
                    var certBase64 = Unprotect(protector, metadata.OAuth2ClientCertificate);
                    var certBytes = Convert.FromBase64String(certBase64);
                    var certPassword = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificatePassword)
                        ? Unprotect(protector, metadata.OAuth2ClientCertificatePassword)
                        : null;

                    try
                    {
                        var token = await _oauth2TokenService.AcquireTokenWithMtlsAsync(
                            metadata.OAuth2TokenEndpoint,
                            metadata.OAuth2ClientId,
                            certBytes,
                            certPassword,
                            metadata.OAuth2Scopes);

                        headers["Authorization"] = $"Bearer {token}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to acquire OAuth2 token via mTLS from '{TokenEndpoint}'.", metadata.OAuth2TokenEndpoint);
                        throw;
                    }
                }
                break;

            case McpClientAuthenticationType.CustomHeaders:
                if (metadata.AdditionalHeaders is not null)
                {
                    foreach (var header in metadata.AdditionalHeaders)
                    {
                        headers[header.Key] = header.Value;
                    }
                }
                break;

            case McpClientAuthenticationType.Anonymous:
            default:
                break;
        }

        return headers;
    }

    private string Unprotect(IDataProtector protector, string value)
    {
        try
        {
            return protector.Unprotect(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect a credential value for MCP SSE connection.");
            return value;
        }
    }
}
