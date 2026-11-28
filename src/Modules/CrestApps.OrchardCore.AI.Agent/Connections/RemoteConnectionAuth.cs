using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.Connections;

/// <summary>
/// The authentication of a remote MCP or A2A connection as the tools handle it: credentials (keys, passwords, client
/// secrets, private keys, certificates and custom header values) are never read out or taken. A tool may switch a
/// connection to anonymous access, which clears every stored credential as the editor does, and change the fields of
/// its current authentication that are not secret. Anything that needs a credential is done on the admin page.
/// </summary>
internal static class RemoteConnectionAuth
{
    /// <summary>
    /// The authentication types a tool may name. Custom headers carry their values as credentials, so they are only
    /// set on the admin page.
    /// </summary>
    public static readonly string[] SettableTypes =
    [
        nameof(ClientAuthenticationType.Anonymous),
        nameof(ClientAuthenticationType.ApiKey),
        nameof(ClientAuthenticationType.Basic),
        nameof(ClientAuthenticationType.OAuth2ClientCredentials),
        nameof(ClientAuthenticationType.OAuth2PrivateKeyJwt),
        nameof(ClientAuthenticationType.OAuth2Mtls),
    ];

    /// <summary>
    /// The arguments of the save tools that set authentication.
    /// </summary>
    public static readonly string[] ArgumentNames =
    [
        "authenticationType",
        "apiKeyHeaderName",
        "apiKeyPrefix",
        "basicUsername",
        "oauth2TokenEndpoint",
        "oauth2ClientId",
        "oauth2Scopes",
        "oauth2KeyId",
    ];

    /// <summary>
    /// The JSON schema of the authentication arguments, to be put inside a tool's properties.
    /// </summary>
    public const string SchemaProperties =
    """
        "authenticationType": {
          "type": "string",
          "enum": ["Anonymous", "ApiKey", "Basic", "OAuth2ClientCredentials", "OAuth2PrivateKeyJwt", "OAuth2Mtls"],
          "description": "How the site signs in to the remote server. Anonymous clears every stored credential. Any other type needs a credential (a key, password, secret, private key or certificate) that a person enters on the admin page the result links to, so it can only be kept, not newly chosen, here."
        },
        "apiKeyHeaderName": {
          "type": "string",
          "description": "ApiKey authentication only: the header the key is sent in, for example Authorization or X-Api-Key."
        },
        "apiKeyPrefix": {
          "type": "string",
          "description": "ApiKey authentication only: the text sent before the key, for example Bearer."
        },
        "basicUsername": {
          "type": "string",
          "description": "Basic authentication only: the user name."
        },
        "oauth2TokenEndpoint": {
          "type": "string",
          "description": "OAuth 2.0 authentication only: the absolute address of the token endpoint. It cannot be moved to another host while a credential is stored."
        },
        "oauth2ClientId": {
          "type": "string",
          "description": "OAuth 2.0 authentication only: the client ID."
        },
        "oauth2Scopes": {
          "type": "string",
          "description": "OAuth 2.0 authentication only: the scopes to ask for, separated by spaces."
        },
        "oauth2KeyId": {
          "type": "string",
          "description": "OAuth 2.0 private key JWT authentication only: the key ID."
        }
    """;

    /// <summary>
    /// Parts of an argument name that mark a credential. No tool of this group takes one.
    /// </summary>
    private static readonly string[] _credentialMarks = ["apikey", "secret", "password", "privatekey", "certificate", "header", "environment", "token", "credential"];

    /// <summary>
    /// Arguments that contain a credential mark but are not credentials.
    /// </summary>
    private static readonly string[] _notCredentials = ["apiKeyHeaderName", "apiKeyPrefix", "oauth2TokenEndpoint"];

    /// <summary>
    /// Finds an argument that looks like a credential, such as an API key or a header value.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <returns>The name of the first such argument, or <see langword="null"/> when there is none.</returns>
    public static string FindCredentialArgument(AIFunctionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return arguments.Keys.FirstOrDefault(name =>
            !_notCredentials.Contains(name, StringComparer.OrdinalIgnoreCase) &&
            _credentialMarks.Any(mark => name.Contains(mark, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Gets the authentication a connection uses as the editor shows it: anonymous access that sends headers is shown
    /// as custom headers.
    /// </summary>
    /// <param name="metadata">The stored authentication.</param>
    /// <returns>The name of the authentication type.</returns>
    public static string GetEffectiveType(IConnectionAuthMetadata metadata)
    {
        if (metadata is null)
        {
            return nameof(ClientAuthenticationType.Anonymous);
        }

        return metadata.AuthenticationType == ClientAuthenticationType.Anonymous && metadata.AdditionalHeaders is { Count: > 0 }
            ? nameof(ClientAuthenticationType.CustomHeaders)
            : metadata.AuthenticationType.ToString();
    }

    /// <summary>
    /// Whether a connection stores any credential, custom header values included.
    /// </summary>
    /// <param name="metadata">The stored authentication.</param>
    /// <returns><see langword="true"/> when something secret is stored.</returns>
    public static bool HasCredential(IConnectionAuthMetadata metadata)
        => metadata is not null &&
            (!string.IsNullOrEmpty(metadata.ApiKey) ||
            !string.IsNullOrEmpty(metadata.BasicPassword) ||
            !string.IsNullOrEmpty(metadata.OAuth2ClientSecret) ||
            !string.IsNullOrEmpty(metadata.OAuth2PrivateKey) ||
            !string.IsNullOrEmpty(metadata.OAuth2ClientCertificate) ||
            !string.IsNullOrEmpty(metadata.OAuth2ClientCertificatePassword) ||
            metadata.AdditionalHeaders is { Count: > 0 });

    /// <summary>
    /// Describes the authentication of a connection: its type and the fields that are not secret, and for each
    /// credential only whether one is stored. Custom headers are listed by name only.
    /// </summary>
    /// <param name="metadata">The stored authentication, or <see langword="null"/>.</param>
    /// <returns>The description.</returns>
    public static object Describe(IConnectionAuthMetadata metadata)
    {
        if (metadata is null)
        {
            return new { type = nameof(ClientAuthenticationType.Anonymous) };
        }

        return new
        {
            type = GetEffectiveType(metadata),
            apiKeyHeaderName = Blank(metadata.ApiKeyHeaderName),
            apiKeyPrefix = Blank(metadata.ApiKeyPrefix),
            hasApiKey = !string.IsNullOrEmpty(metadata.ApiKey),
            basicUsername = Blank(metadata.BasicUsername),
            hasBasicPassword = !string.IsNullOrEmpty(metadata.BasicPassword),
            oauth2TokenEndpoint = Blank(metadata.OAuth2TokenEndpoint),
            oauth2ClientId = Blank(metadata.OAuth2ClientId),
            oauth2Scopes = Blank(metadata.OAuth2Scopes),
            hasOAuth2ClientSecret = !string.IsNullOrEmpty(metadata.OAuth2ClientSecret),
            oauth2KeyId = Blank(metadata.OAuth2KeyId),
            hasOAuth2PrivateKey = !string.IsNullOrEmpty(metadata.OAuth2PrivateKey),
            hasOAuth2ClientCertificate = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificate),
            hasOAuth2ClientCertificatePassword = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificatePassword),
            customHeaderNames = metadata.AdditionalHeaders is { Count: > 0 } headers
                ? headers.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList()
                : null,
        };
    }

    /// <summary>
    /// Whether moving a connection to another address would send its stored credentials to another host.
    /// </summary>
    /// <param name="metadata">The stored authentication.</param>
    /// <param name="currentAddress">The current address.</param>
    /// <param name="newAddress">The new address.</param>
    /// <returns><see langword="true"/> when a credential is stored and the host, scheme or port changes.</returns>
    public static bool MovesCredentials(IConnectionAuthMetadata metadata, string currentAddress, string newAddress)
    {
        if (!HasCredential(metadata) || string.IsNullOrWhiteSpace(currentAddress))
        {
            return false;
        }

        if (!Uri.TryCreate(currentAddress, UriKind.Absolute, out var current) || !Uri.TryCreate(newAddress, UriKind.Absolute, out var next))
        {
            return true;
        }

        return !string.Equals(current.Scheme, next.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(current.IdnHost, next.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            current.Port != next.Port;
    }

    /// <summary>
    /// Applies the authentication arguments of a save tool onto a copy of the stored authentication.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="fields">The copy to change.</param>
    /// <param name="isNew">Whether the connection is being made.</param>
    /// <returns>The outcome: an error to return, or what changed.</returns>
    public static RemoteAuthChange Apply(AIFunctionArguments arguments, RemoteAuthFields fields, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(fields);

        var current = GetEffectiveType(fields);
        var target = current;
        var changes = new List<string>();

        if (arguments.TryGetFirstString("authenticationType", out var typeText) && !string.IsNullOrWhiteSpace(typeText))
        {
            target = SettableTypes.FirstOrDefault(type => string.Equals(type, typeText.Trim(), StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                return RemoteAuthChange.Fail($"authenticationType must be one of {string.Join(", ", SettableTypes)}. Custom headers are set on the admin page.");
            }
        }

        if (!string.Equals(target, current, StringComparison.Ordinal))
        {
            if (target != nameof(ClientAuthenticationType.Anonymous))
            {
                return RemoteAuthChange.NeedsCredential(target);
            }

            fields.Clear();
            changes.Add(isNew ? null : "authentication (now anonymous; stored credentials were removed)");
        }

        var allowed = target switch
        {
            nameof(ClientAuthenticationType.ApiKey) => new[] { "apiKeyHeaderName", "apiKeyPrefix" },
            nameof(ClientAuthenticationType.Basic) => ["basicUsername"],
            nameof(ClientAuthenticationType.OAuth2ClientCredentials) or nameof(ClientAuthenticationType.OAuth2Mtls) => ["oauth2TokenEndpoint", "oauth2ClientId", "oauth2Scopes"],
            nameof(ClientAuthenticationType.OAuth2PrivateKeyJwt) => ["oauth2TokenEndpoint", "oauth2ClientId", "oauth2Scopes", "oauth2KeyId"],
            _ => [],
        };

        foreach (var name in ArgumentNames.Skip(1))
        {
            if (!arguments.TryGetFirstString(name, true, out var value))
            {
                continue;
            }

            if (!allowed.Contains(name, StringComparer.Ordinal))
            {
                return RemoteAuthChange.Fail($"{name} does not apply: the connection uses {target} authentication.");
            }

            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            switch (name)
            {
                case "apiKeyHeaderName":
                    Set(fields.ApiKeyHeaderName, value, changed => fields.ApiKeyHeaderName = changed, name, changes);
                    break;

                case "apiKeyPrefix":
                    Set(fields.ApiKeyPrefix, value, changed => fields.ApiKeyPrefix = changed, name, changes);
                    break;

                case "basicUsername":
                    if (value is null)
                    {
                        return RemoteAuthChange.Fail("Username is required.");
                    }

                    Set(fields.BasicUsername, value, changed => fields.BasicUsername = changed, name, changes);
                    break;

                case "oauth2TokenEndpoint":
                    if (value is null)
                    {
                        return RemoteAuthChange.Fail("Token Endpoint is required.");
                    }

                    if (!Uri.TryCreate(value, UriKind.Absolute, out _))
                    {
                        return RemoteAuthChange.Fail("Invalid Token Endpoint URL.");
                    }

                    if (MovesCredentials(fields, fields.OAuth2TokenEndpoint, value))
                    {
                        return RemoteAuthChange.Fail("the token endpoint cannot be moved to another host while a credential is stored, because the stored credential would be sent there. Change it on the admin page, where the credential is entered again.");
                    }

                    Set(fields.OAuth2TokenEndpoint, value, changed => fields.OAuth2TokenEndpoint = changed, name, changes);
                    break;

                case "oauth2ClientId":
                    if (value is null)
                    {
                        return RemoteAuthChange.Fail("Client ID is required.");
                    }

                    Set(fields.OAuth2ClientId, value, changed => fields.OAuth2ClientId = changed, name, changes);
                    break;

                case "oauth2Scopes":
                    Set(fields.OAuth2Scopes, value, changed => fields.OAuth2Scopes = changed, name, changes);
                    break;

                case "oauth2KeyId":
                    Set(fields.OAuth2KeyId, value, changed => fields.OAuth2KeyId = changed, name, changes);
                    break;
            }
        }

        return RemoteAuthChange.Applied(changes.Where(change => change is not null).ToList());
    }

    private static void Set(string current, string value, Action<string> set, string name, List<string> changes)
    {
        if (string.Equals(current ?? string.Empty, value ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }

        set(value);
        changes.Add(name);
    }

    private static string Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>
/// The outcome of applying authentication arguments.
/// </summary>
/// <param name="Error">The error to return, or <see langword="null"/>.</param>
/// <param name="CredentialType">The authentication type that needs a credential a person must enter, or <see langword="null"/>.</param>
/// <param name="Changes">The fields that changed.</param>
internal sealed record RemoteAuthChange(string Error, string CredentialType, List<string> Changes)
{
    /// <summary>
    /// Gets a value indicating whether the arguments can be saved.
    /// </summary>
    public bool Succeeded => Error is null && CredentialType is null;

    /// <summary>
    /// An argument was wrong.
    /// </summary>
    public static RemoteAuthChange Fail(string error) => new(error, null, []);

    /// <summary>
    /// The chosen authentication needs a credential a person enters on the admin page.
    /// </summary>
    public static RemoteAuthChange NeedsCredential(string type) => new(null, type, []);

    /// <summary>
    /// The arguments were applied.
    /// </summary>
    public static RemoteAuthChange Applied(List<string> changes) => new(null, null, changes);
}

/// <summary>
/// A copy of a remote connection's authentication that a tool changes before writing it back.
/// </summary>
internal sealed class RemoteAuthFields : IConnectionAuthMetadata
{
    /// <inheritdoc/>
    public ClientAuthenticationType AuthenticationType { get; set; }

    /// <inheritdoc/>
    public string ApiKeyHeaderName { get; set; }

    /// <inheritdoc/>
    public string ApiKeyPrefix { get; set; }

    /// <inheritdoc/>
    public string ApiKey { get; set; }

    /// <inheritdoc/>
    public string BasicUsername { get; set; }

    /// <inheritdoc/>
    public string BasicPassword { get; set; }

    /// <inheritdoc/>
    public string OAuth2TokenEndpoint { get; set; }

    /// <inheritdoc/>
    public string OAuth2ClientId { get; set; }

    /// <inheritdoc/>
    public string OAuth2ClientSecret { get; set; }

    /// <inheritdoc/>
    public string OAuth2Scopes { get; set; }

    /// <inheritdoc/>
    public string OAuth2PrivateKey { get; set; }

    /// <inheritdoc/>
    public string OAuth2KeyId { get; set; }

    /// <inheritdoc/>
    public string OAuth2ClientCertificate { get; set; }

    /// <inheritdoc/>
    public string OAuth2ClientCertificatePassword { get; set; }

    /// <inheritdoc/>
    public Dictionary<string, string> AdditionalHeaders { get; set; }

    /// <summary>
    /// Copies stored authentication. The credentials are copied as stored, protected, and written back unchanged.
    /// </summary>
    /// <param name="metadata">The stored authentication, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public static RemoteAuthFields From(IConnectionAuthMetadata metadata)
    {
        var fields = new RemoteAuthFields();

        if (metadata is null)
        {
            return fields;
        }

        fields.AuthenticationType = metadata.AuthenticationType;
        fields.ApiKeyHeaderName = metadata.ApiKeyHeaderName;
        fields.ApiKeyPrefix = metadata.ApiKeyPrefix;
        fields.ApiKey = metadata.ApiKey;
        fields.BasicUsername = metadata.BasicUsername;
        fields.BasicPassword = metadata.BasicPassword;
        fields.OAuth2TokenEndpoint = metadata.OAuth2TokenEndpoint;
        fields.OAuth2ClientId = metadata.OAuth2ClientId;
        fields.OAuth2ClientSecret = metadata.OAuth2ClientSecret;
        fields.OAuth2Scopes = metadata.OAuth2Scopes;
        fields.OAuth2PrivateKey = metadata.OAuth2PrivateKey;
        fields.OAuth2KeyId = metadata.OAuth2KeyId;
        fields.OAuth2ClientCertificate = metadata.OAuth2ClientCertificate;
        fields.OAuth2ClientCertificatePassword = metadata.OAuth2ClientCertificatePassword;
        fields.AdditionalHeaders = metadata.AdditionalHeaders is null ? null : new Dictionary<string, string>(metadata.AdditionalHeaders);

        return fields;
    }

    /// <summary>
    /// Clears every field, credentials included, for anonymous access.
    /// </summary>
    public void Clear()
    {
        AuthenticationType = ClientAuthenticationType.Anonymous;
        ApiKeyHeaderName = null;
        ApiKeyPrefix = null;
        ApiKey = null;
        BasicUsername = null;
        BasicPassword = null;
        OAuth2TokenEndpoint = null;
        OAuth2ClientId = null;
        OAuth2ClientSecret = null;
        OAuth2Scopes = null;
        OAuth2PrivateKey = null;
        OAuth2KeyId = null;
        OAuth2ClientCertificate = null;
        OAuth2ClientCertificatePassword = null;
        AdditionalHeaders = null;
    }

    /// <summary>
    /// Writes the fields onto an MCP server-sent events connection's authentication.
    /// </summary>
    /// <param name="metadata">The authentication to write.</param>
    public void WriteTo(SseMcpConnectionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        metadata.AuthenticationType = AuthenticationType;
        metadata.ApiKeyHeaderName = ApiKeyHeaderName;
        metadata.ApiKeyPrefix = ApiKeyPrefix;
        metadata.ApiKey = ApiKey;
        metadata.BasicUsername = BasicUsername;
        metadata.BasicPassword = BasicPassword;
        metadata.OAuth2TokenEndpoint = OAuth2TokenEndpoint;
        metadata.OAuth2ClientId = OAuth2ClientId;
        metadata.OAuth2ClientSecret = OAuth2ClientSecret;
        metadata.OAuth2Scopes = OAuth2Scopes;
        metadata.OAuth2PrivateKey = OAuth2PrivateKey;
        metadata.OAuth2KeyId = OAuth2KeyId;
        metadata.OAuth2ClientCertificate = OAuth2ClientCertificate;
        metadata.OAuth2ClientCertificatePassword = OAuth2ClientCertificatePassword;
        metadata.AdditionalHeaders = AdditionalHeaders;
    }

    /// <summary>
    /// Writes the fields onto an A2A connection's authentication.
    /// </summary>
    /// <param name="metadata">The authentication to write.</param>
    public void WriteTo(A2AConnectionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        metadata.AuthenticationType = AuthenticationType;
        metadata.ApiKeyHeaderName = ApiKeyHeaderName;
        metadata.ApiKeyPrefix = ApiKeyPrefix;
        metadata.ApiKey = ApiKey;
        metadata.BasicUsername = BasicUsername;
        metadata.BasicPassword = BasicPassword;
        metadata.OAuth2TokenEndpoint = OAuth2TokenEndpoint;
        metadata.OAuth2ClientId = OAuth2ClientId;
        metadata.OAuth2ClientSecret = OAuth2ClientSecret;
        metadata.OAuth2Scopes = OAuth2Scopes;
        metadata.OAuth2PrivateKey = OAuth2PrivateKey;
        metadata.OAuth2KeyId = OAuth2KeyId;
        metadata.OAuth2ClientCertificate = OAuth2ClientCertificate;
        metadata.OAuth2ClientCertificatePassword = OAuth2ClientCertificatePassword;
        metadata.AdditionalHeaders = AdditionalHeaders;
    }
}
