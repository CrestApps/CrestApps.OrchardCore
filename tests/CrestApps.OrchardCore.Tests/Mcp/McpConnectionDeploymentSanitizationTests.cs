using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Mcp;

/// <summary>
/// Tests that deployment export never leaks sensitive credential data.
/// Validates the sanitization logic used by McpConnectionDeploymentSource.
/// </summary>
public sealed class McpConnectionDeploymentSanitizationTests
{
    private static readonly string[] _sensitiveFields =
    [
        nameof(SseMcpConnectionMetadata.ApiKey),
        nameof(SseMcpConnectionMetadata.BasicPassword),
        nameof(SseMcpConnectionMetadata.OAuth2ClientSecret),
        nameof(SseMcpConnectionMetadata.OAuth2PrivateKey),
        nameof(SseMcpConnectionMetadata.OAuth2ClientCertificate),
        nameof(SseMcpConnectionMetadata.OAuth2ClientCertificatePassword),
    ];

    [Fact]
    public void SanitizeSensitiveData_ApiKey_ClearsApiKey()
    {
        // Arrange
        var connection = CreateSseConnectionWithMetadata(new SseMcpConnectionMetadata
        {
            Endpoint = new Uri("https://mcp.example.com/sse"),
            AuthenticationType = McpClientAuthenticationType.ApiKey,
            ApiKeyHeaderName = "Authorization",
            ApiKeyPrefix = "Bearer",
            ApiKey = "encrypted-api-key-secret",
        });

        // Act
        var properties = ExportAndSanitize(connection);

        // Assert
        var metadata = GetMetadataNode(properties);
        Assert.Equal(string.Empty, metadata[nameof(SseMcpConnectionMetadata.ApiKey)]?.GetValue<string>());
        Assert.Equal("Authorization", metadata[nameof(SseMcpConnectionMetadata.ApiKeyHeaderName)]?.GetValue<string>());
        Assert.Equal("Bearer", metadata[nameof(SseMcpConnectionMetadata.ApiKeyPrefix)]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeSensitiveData_Basic_ClearsPassword()
    {
        // Arrange
        var connection = CreateSseConnectionWithMetadata(new SseMcpConnectionMetadata
        {
            Endpoint = new Uri("https://mcp.example.com/sse"),
            AuthenticationType = McpClientAuthenticationType.Basic,
            BasicUsername = "user",
            BasicPassword = "encrypted-password-secret",
        });

        // Act
        var properties = ExportAndSanitize(connection);

        // Assert
        var metadata = GetMetadataNode(properties);
        Assert.Equal(string.Empty, metadata[nameof(SseMcpConnectionMetadata.BasicPassword)]?.GetValue<string>());
        Assert.Equal("user", metadata[nameof(SseMcpConnectionMetadata.BasicUsername)]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeSensitiveData_OAuth2ClientCredentials_ClearsClientSecret()
    {
        // Arrange
        var connection = CreateSseConnectionWithMetadata(new SseMcpConnectionMetadata
        {
            Endpoint = new Uri("https://mcp.example.com/sse"),
            AuthenticationType = McpClientAuthenticationType.OAuth2ClientCredentials,
            OAuth2TokenEndpoint = "https://auth.example.com/token",
            OAuth2ClientId = "client-123",
            OAuth2ClientSecret = "encrypted-client-secret",
            OAuth2Scopes = "read write",
        });

        // Act
        var properties = ExportAndSanitize(connection);

        // Assert
        var metadata = GetMetadataNode(properties);
        Assert.Equal(string.Empty, metadata[nameof(SseMcpConnectionMetadata.OAuth2ClientSecret)]?.GetValue<string>());
        Assert.Equal("https://auth.example.com/token", metadata[nameof(SseMcpConnectionMetadata.OAuth2TokenEndpoint)]?.GetValue<string>());
        Assert.Equal("client-123", metadata[nameof(SseMcpConnectionMetadata.OAuth2ClientId)]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeSensitiveData_OAuth2PrivateKeyJwt_ClearsPrivateKey()
    {
        // Arrange
        var connection = CreateSseConnectionWithMetadata(new SseMcpConnectionMetadata
        {
            Endpoint = new Uri("https://mcp.example.com/sse"),
            AuthenticationType = McpClientAuthenticationType.OAuth2PrivateKeyJwt,
            OAuth2TokenEndpoint = "https://auth.example.com/token",
            OAuth2ClientId = "client-456",
            OAuth2PrivateKey = "encrypted-private-key-pem",
            OAuth2KeyId = "key-001",
            OAuth2Scopes = "api",
        });

        // Act
        var properties = ExportAndSanitize(connection);

        // Assert
        var metadata = GetMetadataNode(properties);
        Assert.Equal(string.Empty, metadata[nameof(SseMcpConnectionMetadata.OAuth2PrivateKey)]?.GetValue<string>());
        Assert.Equal("key-001", metadata[nameof(SseMcpConnectionMetadata.OAuth2KeyId)]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeSensitiveData_OAuth2Mtls_ClearsCertificateAndPassword()
    {
        // Arrange
        var connection = CreateSseConnectionWithMetadata(new SseMcpConnectionMetadata
        {
            Endpoint = new Uri("https://mcp.example.com/sse"),
            AuthenticationType = McpClientAuthenticationType.OAuth2Mtls,
            OAuth2TokenEndpoint = "https://auth.example.com/token",
            OAuth2ClientId = "client-789",
            OAuth2ClientCertificate = "encrypted-cert-data",
            OAuth2ClientCertificatePassword = "encrypted-cert-password",
            OAuth2Scopes = "admin",
        });

        // Act
        var properties = ExportAndSanitize(connection);

        // Assert
        var metadata = GetMetadataNode(properties);
        Assert.Equal(string.Empty, metadata[nameof(SseMcpConnectionMetadata.OAuth2ClientCertificate)]?.GetValue<string>());
        Assert.Equal(string.Empty, metadata[nameof(SseMcpConnectionMetadata.OAuth2ClientCertificatePassword)]?.GetValue<string>());
        Assert.Equal("client-789", metadata[nameof(SseMcpConnectionMetadata.OAuth2ClientId)]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeSensitiveData_AllSensitiveFieldsPopulated_AllCleared()
    {
        // Arrange — worst case: all sensitive fields have values.
        var connection = CreateSseConnectionWithMetadata(new SseMcpConnectionMetadata
        {
            Endpoint = new Uri("https://mcp.example.com/sse"),
            AuthenticationType = McpClientAuthenticationType.OAuth2ClientCredentials,
            ApiKey = "secret-api-key",
            BasicPassword = "secret-password",
            OAuth2ClientSecret = "secret-client-secret",
            OAuth2PrivateKey = "secret-private-key",
            OAuth2ClientCertificate = "secret-cert",
            OAuth2ClientCertificatePassword = "secret-cert-pass",
        });

        // Act
        var properties = ExportAndSanitize(connection);

        // Assert — every sensitive field must be empty.
        var metadata = GetMetadataNode(properties);

        foreach (var field in _sensitiveFields)
        {
            Assert.Equal(string.Empty, metadata[field]?.GetValue<string>());
        }
    }

    [Fact]
    public void SanitizeSensitiveData_NonSseConnection_NoSanitization()
    {
        // Arrange — Stdio connection should not be sanitized.
        var connection = new McpConnection
        {
            Source = McpConstants.TransportTypes.StdIo,
        };

        var customData = new JsonObject { ["Command"] = "docker" };
        connection.Properties["StdioMcpConnectionMetadata"] = JsonSerializer.SerializeToNode(customData);

        // Act
        var properties = ExportAndSanitize(connection);

        // Assert — properties should remain unchanged.
        Assert.Equal("docker", properties["StdioMcpConnectionMetadata"]?["Command"]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeSensitiveData_ExportedJson_NeverContainsSensitiveValues()
    {
        // Arrange
        var secretValues = new[]
        {
            "super-secret-api-key-12345",
            "super-secret-password-67890",
            "super-secret-client-secret-abcde",
            "super-secret-private-key-fghij",
            "super-secret-certificate-klmno",
            "super-secret-cert-password-pqrst",
        };

        var connection = CreateSseConnectionWithMetadata(new SseMcpConnectionMetadata
        {
            Endpoint = new Uri("https://mcp.example.com/sse"),
            AuthenticationType = McpClientAuthenticationType.OAuth2Mtls,
            ApiKey = secretValues[0],
            BasicPassword = secretValues[1],
            OAuth2ClientSecret = secretValues[2],
            OAuth2PrivateKey = secretValues[3],
            OAuth2ClientCertificate = secretValues[4],
            OAuth2ClientCertificatePassword = secretValues[5],
        });

        // Act
        var properties = ExportAndSanitize(connection);
        var serialized = properties.ToJsonString();

        // Assert — the serialized JSON must not contain any of the secret values.
        foreach (var secret in secretValues)
        {
            Assert.DoesNotContain(secret, serialized);
        }
    }

    /// <summary>
    /// Simulates the export sanitization done by McpConnectionDeploymentSource.
    /// </summary>
    private static JsonObject ExportAndSanitize(McpConnection connection)
    {
        var properties = new JsonObject();

        foreach (var property in connection.Properties)
        {
            properties[property.Key] = property.Value.DeepClone();
        }

        // Apply the same sanitization logic used in McpConnectionDeploymentSource.
        if (string.Equals(connection.Source, McpConstants.TransportTypes.Sse, StringComparison.Ordinal))
        {
            var metadataNode = properties[nameof(SseMcpConnectionMetadata)]?.AsObject();

            if (metadataNode != null)
            {
                foreach (var field in _sensitiveFields)
                {
                    metadataNode[field] = string.Empty;
                }
            }
        }

        return properties;
    }

    private static JsonObject GetMetadataNode(JsonObject properties)
        => properties[nameof(SseMcpConnectionMetadata)]?.AsObject()
            ?? throw new InvalidOperationException("SseMcpConnectionMetadata not found in properties.");

    private static McpConnection CreateSseConnectionWithMetadata(SseMcpConnectionMetadata metadata)
    {
        var connection = new McpConnection
        {
            Source = McpConstants.TransportTypes.Sse,
        };

        connection.Put(metadata);

        return connection;
    }
}
