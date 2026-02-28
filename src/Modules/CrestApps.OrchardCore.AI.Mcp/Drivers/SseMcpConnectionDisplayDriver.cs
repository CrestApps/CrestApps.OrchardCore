using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class SseMcpConnectionDisplayDriver : DisplayDriver<McpConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public SseMcpConnectionDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<SseMcpConnectionDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(McpConnection connection, BuildEditorContext context)
    {
        if (connection.Source != McpConstants.TransportTypes.Sse)
        {
            return null;
        }

        return Initialize<SseConnectionFieldsViewModel>("SseMcpConnectionFields_Edit", model =>
        {
            var metadata = connection.As<SseMcpConnectionMetadata>();
            model.Endpoint = metadata.Endpoint?.ToString();
            model.AuthenticationType = metadata.AuthenticationType;

            // Backward compatibility: if no auth type is set but headers exist, show as CustomHeaders.
            if (metadata.AuthenticationType == McpClientAuthenticationType.Anonymous &&
                metadata.AdditionalHeaders is { Count: > 0 })
            {
                model.AuthenticationType = McpClientAuthenticationType.CustomHeaders;
            }

            // API Key fields.
            model.ApiKeyHeaderName = metadata.ApiKeyHeaderName;
            model.ApiKeyPrefix = metadata.ApiKeyPrefix;
            model.HasApiKey = !string.IsNullOrEmpty(metadata.ApiKey);

            // Basic auth fields.
            model.BasicUsername = metadata.BasicUsername;
            model.HasBasicPassword = !string.IsNullOrEmpty(metadata.BasicPassword);

            // OAuth 2.0 fields.
            model.OAuth2TokenEndpoint = metadata.OAuth2TokenEndpoint;
            model.OAuth2ClientId = metadata.OAuth2ClientId;
            model.OAuth2Scopes = metadata.OAuth2Scopes;
            model.HasOAuth2ClientSecret = !string.IsNullOrEmpty(metadata.OAuth2ClientSecret);

            // Private Key JWT fields.
            model.OAuth2KeyId = metadata.OAuth2KeyId;
            model.HasOAuth2PrivateKey = !string.IsNullOrEmpty(metadata.OAuth2PrivateKey);

            // mTLS fields.
            model.HasOAuth2ClientCertificate = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificate);
            model.HasOAuth2ClientCertificatePassword = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificatePassword);

            // Custom headers.
            if (metadata.AdditionalHeaders is not null)
            {
                model.AdditionalHeaders = JsonSerializer.Serialize(metadata.AdditionalHeaders, McpJOptions.SchemaSerializerOptions);
            }

            model.Schema =
            """
            {
              "$schema": "https://json-schema.org/draft-04/schema#",
              "type": "object",
              "additionalProperties": {
                "type": "string"
              }
            }
            """;

        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpConnection connection, UpdateEditorContext context)
    {
        if (connection.Source != McpConstants.TransportTypes.Sse)
        {
            return null;
        }

        var model = new SseConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        Uri endpoint = null;

        if (string.IsNullOrEmpty(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Endpoint field is required."]);
        }
        else if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Invalid Endpoint value."]);
        }

        var metadata = connection.As<SseMcpConnectionMetadata>();
        var protector = _dataProtectionProvider.CreateProtector(McpConstants.DataProtectionPurpose);

        // Preserve existing encrypted values before clearing.
        var existingApiKey = metadata.ApiKey;
        var existingBasicPassword = metadata.BasicPassword;
        var existingOAuth2ClientSecret = metadata.OAuth2ClientSecret;
        var existingOAuth2PrivateKey = metadata.OAuth2PrivateKey;
        var existingOAuth2ClientCertificate = metadata.OAuth2ClientCertificate;
        var existingOAuth2ClientCertificatePassword = metadata.OAuth2ClientCertificatePassword;

        metadata.Endpoint = endpoint;
        metadata.AuthenticationType = model.AuthenticationType;

        // Clear all auth fields, then populate based on selected type.
        ClearAuthFields(metadata);

        switch (model.AuthenticationType)
        {
            case McpClientAuthenticationType.ApiKey:
                ValidateAndPopulateApiKey(context, model, metadata, protector, existingApiKey);
                break;

            case McpClientAuthenticationType.Basic:
                ValidateAndPopulateBasic(context, model, metadata, protector, existingBasicPassword);
                break;

            case McpClientAuthenticationType.OAuth2ClientCredentials:
                ValidateAndPopulateOAuth2(context, model, metadata, protector, existingOAuth2ClientSecret);
                break;

            case McpClientAuthenticationType.OAuth2PrivateKeyJwt:
                ValidateAndPopulateOAuth2PrivateKeyJwt(context, model, metadata, protector, existingOAuth2PrivateKey);
                break;

            case McpClientAuthenticationType.OAuth2Mtls:
                ValidateAndPopulateOAuth2Mtls(context, model, metadata, protector, existingOAuth2ClientCertificate, existingOAuth2ClientCertificatePassword);
                break;

            case McpClientAuthenticationType.CustomHeaders:
                ValidateAndPopulateCustomHeaders(context, model, metadata);
                break;
        }

        connection.Put(metadata);

        return Edit(connection, context);
    }

    private void ValidateAndPopulateApiKey(
        UpdateEditorContext context,
        SseConnectionFieldsViewModel model,
        SseMcpConnectionMetadata metadata,
        IDataProtector protector,
        string existingEncryptedApiKey)
    {
        metadata.ApiKeyHeaderName = model.ApiKeyHeaderName;
        metadata.ApiKeyPrefix = model.ApiKeyPrefix;

        var hasNewKey = !string.IsNullOrWhiteSpace(model.ApiKey);
        var hasExistingKey = !string.IsNullOrEmpty(existingEncryptedApiKey);

        if (!hasExistingKey && !hasNewKey)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["API Key is required."]);
        }

        if (hasNewKey)
        {
            metadata.ApiKey = protector.Protect(model.ApiKey);
        }
        else if (hasExistingKey)
        {
            metadata.ApiKey = existingEncryptedApiKey;
        }
    }

    private void ValidateAndPopulateBasic(
        UpdateEditorContext context,
        SseConnectionFieldsViewModel model,
        SseMcpConnectionMetadata metadata,
        IDataProtector protector,
        string existingEncryptedPassword)
    {
        if (string.IsNullOrWhiteSpace(model.BasicUsername))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BasicUsername), S["Username is required."]);
        }

        metadata.BasicUsername = model.BasicUsername;

        var hasNewPassword = !string.IsNullOrWhiteSpace(model.BasicPassword);
        var hasExistingPassword = !string.IsNullOrEmpty(existingEncryptedPassword);

        if (!hasExistingPassword && !hasNewPassword)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BasicPassword), S["Password is required."]);
        }

        if (hasNewPassword)
        {
            metadata.BasicPassword = protector.Protect(model.BasicPassword);
        }
        else if (hasExistingPassword)
        {
            metadata.BasicPassword = existingEncryptedPassword;
        }
    }

    private void ValidateAndPopulateOAuth2(
        UpdateEditorContext context,
        SseConnectionFieldsViewModel model,
        SseMcpConnectionMetadata metadata,
        IDataProtector protector,
        string existingEncryptedClientSecret)
    {
        ValidateOAuth2CommonFields(context, model, metadata);

        var hasNewSecret = !string.IsNullOrWhiteSpace(model.OAuth2ClientSecret);
        var hasExistingSecret = !string.IsNullOrEmpty(existingEncryptedClientSecret);

        if (!hasExistingSecret && !hasNewSecret)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2ClientSecret), S["Client Secret is required."]);
        }

        if (hasNewSecret)
        {
            metadata.OAuth2ClientSecret = protector.Protect(model.OAuth2ClientSecret);
        }
        else if (hasExistingSecret)
        {
            metadata.OAuth2ClientSecret = existingEncryptedClientSecret;
        }
    }

    private void ValidateAndPopulateOAuth2PrivateKeyJwt(
        UpdateEditorContext context,
        SseConnectionFieldsViewModel model,
        SseMcpConnectionMetadata metadata,
        IDataProtector protector,
        string existingEncryptedPrivateKey)
    {
        ValidateOAuth2CommonFields(context, model, metadata);

        metadata.OAuth2KeyId = model.OAuth2KeyId;

        var hasNewKey = !string.IsNullOrWhiteSpace(model.OAuth2PrivateKey);
        var hasExistingKey = !string.IsNullOrEmpty(existingEncryptedPrivateKey);

        if (!hasExistingKey && !hasNewKey)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2PrivateKey), S["Private Key is required."]);
        }

        if (hasNewKey)
        {
            metadata.OAuth2PrivateKey = protector.Protect(model.OAuth2PrivateKey);
        }
        else if (hasExistingKey)
        {
            metadata.OAuth2PrivateKey = existingEncryptedPrivateKey;
        }
    }

    private void ValidateAndPopulateOAuth2Mtls(
        UpdateEditorContext context,
        SseConnectionFieldsViewModel model,
        SseMcpConnectionMetadata metadata,
        IDataProtector protector,
        string existingEncryptedCertificate,
        string existingEncryptedCertificatePassword)
    {
        ValidateOAuth2CommonFields(context, model, metadata);

        var hasNewCert = !string.IsNullOrWhiteSpace(model.OAuth2ClientCertificate);
        var hasExistingCert = !string.IsNullOrEmpty(existingEncryptedCertificate);

        if (!hasExistingCert && !hasNewCert)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2ClientCertificate), S["Client Certificate is required."]);
        }

        if (hasNewCert)
        {
            metadata.OAuth2ClientCertificate = protector.Protect(model.OAuth2ClientCertificate);
        }
        else if (hasExistingCert)
        {
            metadata.OAuth2ClientCertificate = existingEncryptedCertificate;
        }

        var hasNewPassword = !string.IsNullOrWhiteSpace(model.OAuth2ClientCertificatePassword);

        if (hasNewPassword)
        {
            metadata.OAuth2ClientCertificatePassword = protector.Protect(model.OAuth2ClientCertificatePassword);
        }
        else if (!string.IsNullOrEmpty(existingEncryptedCertificatePassword))
        {
            metadata.OAuth2ClientCertificatePassword = existingEncryptedCertificatePassword;
        }
    }

    private void ValidateAndPopulateCustomHeaders(
        UpdateEditorContext context,
        SseConnectionFieldsViewModel model,
        SseMcpConnectionMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(model.AdditionalHeaders))
        {
            try
            {
                metadata.AdditionalHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(model.AdditionalHeaders);
            }
            catch
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdditionalHeaders), S["Invalid additional headers format."]);
            }
        }
    }

    private void ValidateOAuth2CommonFields(
        UpdateEditorContext context,
        SseConnectionFieldsViewModel model,
        SseMcpConnectionMetadata metadata)
    {
        if (string.IsNullOrEmpty(model.OAuth2TokenEndpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2TokenEndpoint), S["Token Endpoint is required."]);
        }
        else if (!Uri.TryCreate(model.OAuth2TokenEndpoint, UriKind.Absolute, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2TokenEndpoint), S["Invalid Token Endpoint URL."]);
        }

        if (string.IsNullOrWhiteSpace(model.OAuth2ClientId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OAuth2ClientId), S["Client ID is required."]);
        }

        metadata.OAuth2TokenEndpoint = model.OAuth2TokenEndpoint;
        metadata.OAuth2ClientId = model.OAuth2ClientId;
        metadata.OAuth2Scopes = model.OAuth2Scopes;
    }

    private static void ClearAuthFields(SseMcpConnectionMetadata metadata)
    {
        metadata.ApiKeyHeaderName = null;
        metadata.ApiKeyPrefix = null;
        metadata.ApiKey = null;
        metadata.BasicUsername = null;
        metadata.BasicPassword = null;
        metadata.OAuth2TokenEndpoint = null;
        metadata.OAuth2ClientId = null;
        metadata.OAuth2ClientSecret = null;
        metadata.OAuth2Scopes = null;
        metadata.OAuth2PrivateKey = null;
        metadata.OAuth2KeyId = null;
        metadata.OAuth2ClientCertificate = null;
        metadata.OAuth2ClientCertificatePassword = null;
        metadata.AdditionalHeaders = null;
    }
}
