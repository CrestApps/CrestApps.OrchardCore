using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Core.Drivers;

/// <summary>
/// The base of an editor for a remote connection that authenticates with <see cref="IConnectionAuthMetadata"/>, such as
/// an A2A agent or an SSE MCP server. It fills the authentication fields from the stored metadata and applies what the
/// form posted: only the fields of the chosen authentication type are kept, each secret is protected and a blank secret
/// keeps the stored one.
/// </summary>
/// <typeparam name="TModel">The connection type the driver edits.</typeparam>
public abstract class ConnectionAuthDisplayDriverBase<TModel> : DisplayDriver<TModel>
    where TModel : class
{
    /// <summary>
    /// The JSON schema of the additional headers editor: an object of string values.
    /// </summary>
    public const string AdditionalHeadersSchema =
    """
{
  "$schema": "https://json-schema.org/draft-04/schema#",
  "type": "object",
  "additionalProperties": {
    "type": "string"
  }
}
""";

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly string _protectionPurpose;
    private readonly IAIEndpointPolicy _endpointPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionAuthDisplayDriverBase{TModel}"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The provider the secrets are protected with.</param>
    /// <param name="protectionPurpose">The purpose the secrets of this connection type are protected for.</param>
    /// <param name="stringLocalizer">The localizer of the derived driver, which owns the validation messages.</param>
    protected ConnectionAuthDisplayDriverBase(
        IDataProtectionProvider dataProtectionProvider,
        string protectionPurpose,
        IStringLocalizer stringLocalizer)
        : this(dataProtectionProvider, protectionPurpose, stringLocalizer, endpointPolicy: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionAuthDisplayDriverBase{TModel}"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The provider the secrets are protected with.</param>
    /// <param name="protectionPurpose">The purpose the secrets of this connection type are protected for.</param>
    /// <param name="stringLocalizer">The localizer of the derived driver, which owns the validation messages.</param>
    /// <param name="endpointPolicy">The policy an OAuth2 token endpoint is checked with, or <see langword="null"/> to
    /// refuse every address on the site's own network.</param>
    protected ConnectionAuthDisplayDriverBase(
        IDataProtectionProvider dataProtectionProvider,
        string protectionPurpose,
        IStringLocalizer stringLocalizer,
        IAIEndpointPolicy endpointPolicy)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectionPurpose);

        _dataProtectionProvider = dataProtectionProvider;
        _protectionPurpose = protectionPurpose;
        _endpointPolicy = endpointPolicy;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the localizer of the derived driver.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <summary>
    /// Fills the authentication fields of the editor from the stored metadata. Secrets are never shown; their
    /// <c>Has…</c> flags say whether one is saved. A connection that stores headers without an authentication type is
    /// shown with custom headers.
    /// </summary>
    /// <param name="metadata">The stored authentication metadata.</param>
    /// <param name="model">The editor model to fill.</param>
    /// <param name="headersSerializerOptions">The options the additional headers are written with.</param>
    protected static void PopulateAuthFields(IConnectionAuthMetadata metadata, ConnectionAuthFieldsViewModel model, JsonSerializerOptions headersSerializerOptions)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(model);

        model.AuthenticationType = metadata.AuthenticationType;

        if (metadata.AuthenticationType == ClientAuthenticationType.Anonymous &&
            metadata.AdditionalHeaders is { Count: > 0 })
        {
            model.AuthenticationType = ClientAuthenticationType.CustomHeaders;
        }

        model.ApiKeyHeaderName = metadata.ApiKeyHeaderName;
        model.ApiKeyPrefix = metadata.ApiKeyPrefix;
        model.HasApiKey = ProtectedSettingExtensions.HasSecret(metadata.ApiKey);

        model.BasicUsername = metadata.BasicUsername;
        model.HasBasicPassword = ProtectedSettingExtensions.HasSecret(metadata.BasicPassword);

        model.OAuth2TokenEndpoint = metadata.OAuth2TokenEndpoint;
        model.OAuth2ClientId = metadata.OAuth2ClientId;
        model.OAuth2Scopes = metadata.OAuth2Scopes;
        model.HasOAuth2ClientSecret = ProtectedSettingExtensions.HasSecret(metadata.OAuth2ClientSecret);

        model.OAuth2KeyId = metadata.OAuth2KeyId;
        model.HasOAuth2PrivateKey = ProtectedSettingExtensions.HasSecret(metadata.OAuth2PrivateKey);

        model.HasOAuth2ClientCertificate = ProtectedSettingExtensions.HasSecret(metadata.OAuth2ClientCertificate);
        model.HasOAuth2ClientCertificatePassword = ProtectedSettingExtensions.HasSecret(metadata.OAuth2ClientCertificatePassword);

        if (metadata.AdditionalHeaders is not null)
        {
            model.AdditionalHeaders = JsonSerializer.Serialize(metadata.AdditionalHeaders, headersSerializerOptions);
        }

        model.Schema = AdditionalHeadersSchema;
    }

    /// <summary>
    /// Applies the posted authentication fields onto the stored metadata: the fields of other authentication types are
    /// cleared, the fields of the chosen type are validated, and each secret is protected or, when left blank, kept.
    /// </summary>
    /// <param name="context">The editor update context the validation errors are added to.</param>
    /// <param name="model">The posted editor model.</param>
    /// <param name="metadata">The stored authentication metadata to update.</param>
    protected void ApplyAuthFields(UpdateEditorContext context, ConnectionAuthFieldsViewModel model, IConnectionAuthMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(metadata);

        // Only the fields of the chosen authentication type are kept; every other field is cleared.
        var values = new ConnectionAuthValues
        {
            AuthenticationType = model.AuthenticationType,
        };

        switch (model.AuthenticationType)
        {
            case ClientAuthenticationType.ApiKey:
                values.ApiKeyHeaderName = model.ApiKeyHeaderName;
                values.ApiKeyPrefix = model.ApiKeyPrefix;
                values.ApiKey = ApplyRequiredSecret(context, metadata.ApiKey, model.ApiKey, nameof(model.ApiKey), S["API Key is required."]);
                break;

            case ClientAuthenticationType.Basic:
                if (string.IsNullOrWhiteSpace(model.BasicUsername))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.BasicUsername), S["Username is required."]);
                }

                values.BasicUsername = model.BasicUsername;
                values.BasicPassword = ApplyRequiredSecret(context, metadata.BasicPassword, model.BasicPassword, nameof(model.BasicPassword), S["Password is required."]);
                break;

            case ClientAuthenticationType.OAuth2ClientCredentials:
                ApplyOAuth2CommonFields(context, model, values);
                values.OAuth2ClientSecret = ApplyRequiredSecret(context, metadata.OAuth2ClientSecret, model.OAuth2ClientSecret, nameof(model.OAuth2ClientSecret), S["Client Secret is required."]);
                break;

            case ClientAuthenticationType.OAuth2PrivateKeyJwt:
                ApplyOAuth2CommonFields(context, model, values);
                values.OAuth2KeyId = model.OAuth2KeyId;
                values.OAuth2PrivateKey = ApplyRequiredSecret(context, metadata.OAuth2PrivateKey, model.OAuth2PrivateKey, nameof(model.OAuth2PrivateKey), S["Private Key is required."]);
                break;

            case ClientAuthenticationType.OAuth2Mtls:
                ApplyOAuth2CommonFields(context, model, values);
                values.OAuth2ClientCertificate = ApplyRequiredSecret(context, metadata.OAuth2ClientCertificate, model.OAuth2ClientCertificate, nameof(model.OAuth2ClientCertificate), S["Client Certificate is required."]);
                values.OAuth2ClientCertificatePassword = _dataProtectionProvider.ApplySecret(_protectionPurpose, metadata.OAuth2ClientCertificatePassword, model.OAuth2ClientCertificatePassword);
                break;

            case ClientAuthenticationType.CustomHeaders:
                ApplyCustomHeaders(context, model, values);
                break;
        }

        values.CopyTo(metadata);
    }

    private string ApplyRequiredSecret(UpdateEditorContext context, string existing, string posted, string fieldName, LocalizedString requiredMessage)
    {
        var value = _dataProtectionProvider.ApplySecret(_protectionPurpose, existing, posted);

        if (!ProtectedSettingExtensions.HasSecret(value))
        {
            context.Updater.ModelState.AddModelError(Prefix, fieldName, requiredMessage);
        }

        return value;
    }

    private void ApplyOAuth2CommonFields(UpdateEditorContext context, ConnectionAuthFieldsViewModel model, ConnectionAuthValues metadata)
    {
        if (string.IsNullOrEmpty(model.OAuth2TokenEndpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2TokenEndpoint), S["Token Endpoint is required."]);
        }
        else if (!Uri.TryCreate(model.OAuth2TokenEndpoint, UriKind.Absolute, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2TokenEndpoint), S["Invalid Token Endpoint URL."]);
        }
        else if (!(_endpointPolicy?.IsAllowed(model.OAuth2TokenEndpoint) ?? AIEgress.IsAllowedEndpoint(model.OAuth2TokenEndpoint, isDevelopment: false, options: null)))
        {
            // The server posts the client's credentials here and sends the token it answers with to the connection, so
            // it is held to the same rule as the connection's own endpoint.
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2TokenEndpoint), S["This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections."]);
        }

        if (string.IsNullOrWhiteSpace(model.OAuth2ClientId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2ClientId), S["Client ID is required."]);
        }

        metadata.OAuth2TokenEndpoint = model.OAuth2TokenEndpoint;
        metadata.OAuth2ClientId = model.OAuth2ClientId;
        metadata.OAuth2Scopes = model.OAuth2Scopes;
    }

    private void ApplyCustomHeaders(UpdateEditorContext context, ConnectionAuthFieldsViewModel model, ConnectionAuthValues metadata)
    {
        if (string.IsNullOrWhiteSpace(model.AdditionalHeaders))
        {
            return;
        }

        try
        {
            metadata.AdditionalHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(model.AdditionalHeaders);
        }
        catch (JsonException)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdditionalHeaders), S["Invalid additional headers format."]);
        }
    }
}
