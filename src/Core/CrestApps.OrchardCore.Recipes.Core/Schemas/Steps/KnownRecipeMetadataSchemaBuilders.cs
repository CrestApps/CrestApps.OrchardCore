using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

internal static class KnownRecipeMetadataSchemaBuilders
{
    internal static JsonSchemaBuilder BuildA2AConnectionMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known A2A client authentication settings stored for the connection. Additional properties are allowed for future A2A transport extensions.")
            .Properties(
                ("AuthenticationType", BuildClientAuthenticationTypeSchema("How the A2A client authenticates to the remote endpoint.")),
                ("ApiKeyHeaderName", RecipeStepSchemaBuilders.String().Description("Header name that carries the API key when AuthenticationType is ApiKey.")),
                ("ApiKeyPrefix", RecipeStepSchemaBuilders.String().Description("Optional prefix placed before the API key value, such as 'Bearer' or a vendor-specific prefix.")),
                ("ApiKey", RecipeStepSchemaBuilders.String().Description("API key secret. Recipe exports may sanitize this value, so imports can leave it empty to keep an existing stored secret.")),
                ("BasicUsername", RecipeStepSchemaBuilders.String().Description("Username used when AuthenticationType is Basic.")),
                ("BasicPassword", RecipeStepSchemaBuilders.String().Description("Password used when AuthenticationType is Basic. Recipe exports may sanitize this value.")),
                ("OAuth2TokenEndpoint", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 token endpoint used by client-credentials, private-key JWT, or mutual-TLS authentication.")),
                ("OAuth2ClientId", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 client identifier.")),
                ("OAuth2ClientSecret", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 client secret for client-credentials authentication. Recipe exports may sanitize this value.")),
                ("OAuth2Scopes", RecipeStepSchemaBuilders.String().Description("Optional OAuth 2.0 scopes, typically space-delimited.")),
                ("OAuth2KeyId", RecipeStepSchemaBuilders.String().Description("Optional key identifier used when AuthenticationType is OAuth2PrivateKeyJwt.")),
                ("OAuth2PrivateKey", RecipeStepSchemaBuilders.String().Description("Private key material used for OAuth2PrivateKeyJwt. Recipe exports may sanitize this value.")),
                ("OAuth2ClientCertificate", RecipeStepSchemaBuilders.String().Description("Client certificate payload used for OAuth2Mtls. Recipe exports may sanitize this value.")),
                ("OAuth2ClientCertificatePassword", RecipeStepSchemaBuilders.String().Description("Password for the client certificate when AuthenticationType is OAuth2Mtls. Recipe exports may sanitize this value.")),
                ("AdditionalHeaders", BuildAdditionalHeadersSchema("Additional HTTP headers sent with each request when AuthenticationType is CustomHeaders.")))
            .AdditionalProperties(true);

    internal static JsonSchemaBuilder BuildFtpConnectionMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known FTP connection settings for ftp:// MCP resources. Additional properties are allowed for provider-specific extensions.")
            .Properties(
                ("Host", RecipeStepSchemaBuilders.String().Description("FTP or FTPS server host name.")),
                ("Port", RecipeStepSchemaBuilders.Integer().Description("Optional FTP server port. Common defaults are 21 for FTP and 990 for implicit FTPS.")),
                ("Username", RecipeStepSchemaBuilders.String().Description("Username used to authenticate to the FTP server.")),
                ("Password", RecipeStepSchemaBuilders.String().Description("Password used to authenticate to the FTP server. Recipe exports may sanitize this value.")),
                ("EncryptionMode", RecipeStepSchemaBuilders.String()
                    .Enum("None", "Implicit", "Explicit", "Auto")
                    .Description("FTP encryption mode. Use None for plain FTP, Explicit or Implicit for FTPS, or Auto to let the client decide.")),
                ("DataConnectionType", RecipeStepSchemaBuilders.String()
                    .Enum("AutoPassive", "PASV", "PASVEX", "EPSV", "AutoActive", "PORT", "EPRT")
                    .Description("FTP data channel mode used for directory listings and file transfers.")),
                ("ValidateAnyCertificate", RecipeStepSchemaBuilders.Boolean().Description("Whether to accept any SSL/TLS certificate, including self-signed certificates.")),
                ("ConnectTimeout", RecipeStepSchemaBuilders.Integer().Description("Connection timeout in seconds.")),
                ("ReadTimeout", RecipeStepSchemaBuilders.Integer().Description("Read timeout in seconds for FTP operations.")),
                ("RetryAttempts", RecipeStepSchemaBuilders.Integer().Description("How many times failed FTP operations should be retried.")))
            .AdditionalProperties(true);

    internal static JsonSchemaBuilder BuildSftpConnectionMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known SFTP connection settings for sftp:// MCP resources. Additional properties are allowed for provider-specific extensions.")
            .Properties(
                ("Host", RecipeStepSchemaBuilders.String().Description("SFTP server host name.")),
                ("Port", RecipeStepSchemaBuilders.Integer().Description("Optional SFTP server port. The usual default is 22.")),
                ("Username", RecipeStepSchemaBuilders.String().Description("Username used to authenticate to the SFTP server.")),
                ("Password", RecipeStepSchemaBuilders.String().Description("Password used for SFTP password authentication. Recipe exports may sanitize this value.")),
                ("PrivateKey", RecipeStepSchemaBuilders.String().Description("Private key payload used for SFTP key-based authentication. Recipe exports may sanitize this value.")),
                ("Passphrase", RecipeStepSchemaBuilders.String().Description("Optional passphrase for the private key. Recipe exports may sanitize this value.")),
                ("ProxyType", RecipeStepSchemaBuilders.String()
                    .Enum("None", "Socks4", "Socks5", "Http")
                    .Description("Optional proxy type used when the SFTP server is reached through a proxy.")),
                ("ProxyHost", RecipeStepSchemaBuilders.String().Description("Proxy host name when ProxyType is not None.")),
                ("ProxyPort", RecipeStepSchemaBuilders.Integer().Description("Proxy port when ProxyType is not None.")),
                ("ProxyUsername", RecipeStepSchemaBuilders.String().Description("Optional proxy username.")),
                ("ProxyPassword", RecipeStepSchemaBuilders.String().Description("Optional proxy password. Recipe exports may sanitize this value.")),
                ("ConnectionTimeout", RecipeStepSchemaBuilders.Integer().Description("Connection timeout in seconds.")),
                ("KeepAliveInterval", RecipeStepSchemaBuilders.Integer().Description("Optional keep-alive interval in seconds to keep idle SFTP sessions active.")))
            .AdditionalProperties(true);

    internal static JsonSchemaBuilder BuildSseMcpConnectionMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known HTTP/SSE MCP client settings for remote MCP servers. Additional properties are allowed for future MCP client features.")
            .Properties(
                ("Endpoint", RecipeStepSchemaBuilders.String().Description("Absolute HTTP or HTTPS endpoint of the remote MCP server.")),
                ("AuthenticationType", BuildClientAuthenticationTypeSchema("How the SSE MCP client authenticates to the remote server.")),
                ("ApiKeyHeaderName", RecipeStepSchemaBuilders.String().Description("Header name that carries the API key when AuthenticationType is ApiKey.")),
                ("ApiKeyPrefix", RecipeStepSchemaBuilders.String().Description("Optional prefix placed before the API key value, such as 'Bearer'.")),
                ("ApiKey", RecipeStepSchemaBuilders.String().Description("API key secret. Recipe exports may sanitize this value.")),
                ("BasicUsername", RecipeStepSchemaBuilders.String().Description("Username used when AuthenticationType is Basic.")),
                ("BasicPassword", RecipeStepSchemaBuilders.String().Description("Password used when AuthenticationType is Basic. Recipe exports may sanitize this value.")),
                ("OAuth2TokenEndpoint", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 token endpoint used by client-credentials, private-key JWT, or mutual-TLS authentication.")),
                ("OAuth2ClientId", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 client identifier.")),
                ("OAuth2ClientSecret", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 client secret for client-credentials authentication. Recipe exports may sanitize this value.")),
                ("OAuth2Scopes", RecipeStepSchemaBuilders.String().Description("Optional OAuth 2.0 scopes, typically space-delimited.")),
                ("OAuth2KeyId", RecipeStepSchemaBuilders.String().Description("Optional key identifier used when AuthenticationType is OAuth2PrivateKeyJwt.")),
                ("OAuth2PrivateKey", RecipeStepSchemaBuilders.String().Description("Private key material used for OAuth2PrivateKeyJwt. Recipe exports may sanitize this value.")),
                ("OAuth2ClientCertificate", RecipeStepSchemaBuilders.String().Description("Client certificate payload used for OAuth2Mtls. Recipe exports may sanitize this value.")),
                ("OAuth2ClientCertificatePassword", RecipeStepSchemaBuilders.String().Description("Password for the client certificate when AuthenticationType is OAuth2Mtls. Recipe exports may sanitize this value.")),
                ("AdditionalHeaders", BuildAdditionalHeadersSchema("Additional HTTP headers sent with each request when AuthenticationType is CustomHeaders.")))
            .AdditionalProperties(true);

    internal static JsonSchemaBuilder BuildStdioMcpConnectionMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known STDIO MCP client settings for launching a local MCP process. Additional properties are allowed for future transport options.")
            .Properties(
                ("Command", RecipeStepSchemaBuilders.String().Description("Executable or shell command used to start the local MCP server.")),
                ("Arguments", RecipeStepSchemaBuilders.StringArray().Description("Command-line arguments passed to the local MCP server process.")),
                ("WorkingDirectory", RecipeStepSchemaBuilders.String().Description("Optional working directory used when starting the process.")),
                ("EnvironmentVariables", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("Environment variables injected into the local MCP server process. Keys are variable names and values are strings.")
                    .AdditionalProperties(RecipeStepSchemaBuilders.String())))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAdditionalHeadersSchema(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(description)
            .AdditionalProperties(RecipeStepSchemaBuilders.String());

    private static JsonSchemaBuilder BuildClientAuthenticationTypeSchema(string description)
        => RecipeStepSchemaBuilders.String()
            .Enum("Anonymous", "ApiKey", "Basic", "OAuth2ClientCredentials", "OAuth2PrivateKeyJwt", "OAuth2Mtls", "CustomHeaders")
            .Description(description);
}
