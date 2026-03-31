using System.Text.Json;
using CrestApps.AI.Mcp;
using CrestApps.AI.Mcp.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class McpConnectionController : Controller
{
    private static readonly JsonSerializerOptions _indentedJsonOptions = new() { WriteIndented = true };

    private readonly ICatalog<McpConnection> _catalog;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly TimeProvider _timeProvider;

    public McpConnectionController(
        ICatalog<McpConnection> catalog,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider)
    {
        _catalog = catalog;
        _dataProtectionProvider = dataProtectionProvider;
        _timeProvider = timeProvider;
    }

    public async Task<IActionResult> Index()
        => View((await _catalog.GetAllAsync())
            .OrderBy(connection => connection.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public IActionResult Create()
        => View(new McpConnectionViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(McpConnectionViewModel model)
    {
        Validate(model, false);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var connection = new McpConnection
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime,
        };

        Apply(model, connection);

        await _catalog.CreateAsync(connection);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var connection = await _catalog.FindByIdAsync(id);
        if (connection == null)
        {
            return NotFound();
        }

        return View(ToViewModel(connection));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(McpConnectionViewModel model)
    {
        var connection = await _catalog.FindByIdAsync(model.ItemId);
        if (connection == null)
        {
            return NotFound();
        }

        Validate(model, true);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Apply(model, connection);

        await _catalog.UpdateAsync(connection);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var connection = await _catalog.FindByIdAsync(id);
        if (connection == null)
        {
            return NotFound();
        }

        await _catalog.DeleteAsync(connection);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private void Validate(McpConnectionViewModel model, bool isEditing)
    {
        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            ModelState.AddModelError(nameof(model.DisplayText), "Display text is required.");
        }

        if (model.Source == McpConstants.TransportTypes.Sse)
        {
            if (string.IsNullOrWhiteSpace(model.Endpoint))
            {
                ModelState.AddModelError(nameof(model.Endpoint), "Endpoint is required.");
            }
            else if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out _))
            {
                ModelState.AddModelError(nameof(model.Endpoint), "Endpoint must be a valid absolute URL.");
            }

            switch (model.AuthenticationType)
            {
                case McpClientAuthenticationType.ApiKey:
                    if ((!isEditing || !model.HasApiKey) && string.IsNullOrWhiteSpace(model.ApiKey))
                    {
                        ModelState.AddModelError(nameof(model.ApiKey), "API key is required.");
                    }
                    break;

                case McpClientAuthenticationType.Basic:
                    if (string.IsNullOrWhiteSpace(model.BasicUsername))
                    {
                        ModelState.AddModelError(nameof(model.BasicUsername), "Username is required.");
                    }

                    if ((!isEditing || !model.HasBasicPassword) && string.IsNullOrWhiteSpace(model.BasicPassword))
                    {
                        ModelState.AddModelError(nameof(model.BasicPassword), "Password is required.");
                    }
                    break;

                case McpClientAuthenticationType.OAuth2ClientCredentials:
                    ValidateOAuth2Common(model);
                    if ((!isEditing || !model.HasOAuth2ClientSecret) && string.IsNullOrWhiteSpace(model.OAuth2ClientSecret))
                    {
                        ModelState.AddModelError(nameof(model.OAuth2ClientSecret), "Client secret is required.");
                    }
                    break;

                case McpClientAuthenticationType.OAuth2PrivateKeyJwt:
                    ValidateOAuth2Common(model);
                    if ((!isEditing || !model.HasOAuth2PrivateKey) && string.IsNullOrWhiteSpace(model.OAuth2PrivateKey))
                    {
                        ModelState.AddModelError(nameof(model.OAuth2PrivateKey), "Private key is required.");
                    }
                    break;

                case McpClientAuthenticationType.OAuth2Mtls:
                    ValidateOAuth2Common(model);
                    if ((!isEditing || !model.HasOAuth2ClientCertificate) && string.IsNullOrWhiteSpace(model.OAuth2ClientCertificate))
                    {
                        ModelState.AddModelError(nameof(model.OAuth2ClientCertificate), "Client certificate is required.");
                    }
                    break;

                case McpClientAuthenticationType.CustomHeaders:
                    if (!string.IsNullOrWhiteSpace(model.AdditionalHeaders))
                    {
                        try
                        {
                            _ = JsonSerializer.Deserialize<Dictionary<string, string>>(model.AdditionalHeaders);
                        }
                        catch (JsonException)
                        {
                            ModelState.AddModelError(nameof(model.AdditionalHeaders), "Additional headers must be valid JSON.");
                        }
                    }
                    break;
            }
        }
        else if (model.Source == McpConstants.TransportTypes.StdIo)
        {
            if (string.IsNullOrWhiteSpace(model.Command))
            {
                ModelState.AddModelError(nameof(model.Command), "Command is required.");
            }

            ValidateJsonArray(model.Arguments, nameof(model.Arguments), "Arguments must be a valid JSON string array.");
            ValidateJsonObject(model.EnvironmentVariables, nameof(model.EnvironmentVariables), "Environment variables must be valid JSON.");
        }
        else
        {
            ModelState.AddModelError(nameof(model.Source), "Connection type is not supported.");
        }
    }

    private void ValidateOAuth2Common(McpConnectionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.OAuth2TokenEndpoint))
        {
            ModelState.AddModelError(nameof(model.OAuth2TokenEndpoint), "Token endpoint is required.");
        }
        else if (!Uri.TryCreate(model.OAuth2TokenEndpoint, UriKind.Absolute, out _))
        {
            ModelState.AddModelError(nameof(model.OAuth2TokenEndpoint), "Token endpoint must be a valid absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(model.OAuth2ClientId))
        {
            ModelState.AddModelError(nameof(model.OAuth2ClientId), "Client ID is required.");
        }
    }

    private void ValidateJsonArray(string value, string fieldName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            _ = JsonSerializer.Deserialize<string[]>(value);
        }
        catch (JsonException)
        {
            ModelState.AddModelError(fieldName, message);
        }
    }

    private void ValidateJsonObject(string value, string fieldName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            _ = JsonSerializer.Deserialize<Dictionary<string, string>>(value);
        }
        catch (JsonException)
        {
            ModelState.AddModelError(fieldName, message);
        }
    }

    private void Apply(McpConnectionViewModel model, McpConnection connection)
    {
        connection.DisplayText = model.DisplayText.Trim();
        connection.Source = model.Source;

        if (model.Source == McpConstants.TransportTypes.Sse)
        {
            var metadata = connection.As<SseMcpConnectionMetadata>();
            var protector = _dataProtectionProvider.CreateProtector(McpConstants.DataProtectionPurpose);
            var existingApiKey = metadata.ApiKey;
            var existingBasicPassword = metadata.BasicPassword;
            var existingOAuth2ClientSecret = metadata.OAuth2ClientSecret;
            var existingOAuth2PrivateKey = metadata.OAuth2PrivateKey;
            var existingCertificate = metadata.OAuth2ClientCertificate;
            var existingCertificatePassword = metadata.OAuth2ClientCertificatePassword;

            metadata.Endpoint = Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var endpoint) ? endpoint : null;
            metadata.AuthenticationType = model.AuthenticationType;
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

            switch (model.AuthenticationType)
            {
                case McpClientAuthenticationType.ApiKey:
                    metadata.ApiKeyHeaderName = model.ApiKeyHeaderName?.Trim();
                    metadata.ApiKeyPrefix = model.ApiKeyPrefix?.Trim();
                    metadata.ApiKey = ProtectOrReuse(model.ApiKey, existingApiKey, protector);
                    break;
                case McpClientAuthenticationType.Basic:
                    metadata.BasicUsername = model.BasicUsername?.Trim();
                    metadata.BasicPassword = ProtectOrReuse(model.BasicPassword, existingBasicPassword, protector);
                    break;
                case McpClientAuthenticationType.OAuth2ClientCredentials:
                    PopulateOAuthCommon(model, metadata);
                    metadata.OAuth2ClientSecret = ProtectOrReuse(model.OAuth2ClientSecret, existingOAuth2ClientSecret, protector);
                    break;
                case McpClientAuthenticationType.OAuth2PrivateKeyJwt:
                    PopulateOAuthCommon(model, metadata);
                    metadata.OAuth2KeyId = model.OAuth2KeyId?.Trim();
                    metadata.OAuth2PrivateKey = ProtectOrReuse(model.OAuth2PrivateKey, existingOAuth2PrivateKey, protector);
                    break;
                case McpClientAuthenticationType.OAuth2Mtls:
                    PopulateOAuthCommon(model, metadata);
                    metadata.OAuth2ClientCertificate = ProtectOrReuse(model.OAuth2ClientCertificate, existingCertificate, protector);
                    metadata.OAuth2ClientCertificatePassword = ProtectOrReuse(model.OAuth2ClientCertificatePassword, existingCertificatePassword, protector);
                    break;
                case McpClientAuthenticationType.CustomHeaders:
                    metadata.AdditionalHeaders = string.IsNullOrWhiteSpace(model.AdditionalHeaders)
                        ? []
                        : JsonSerializer.Deserialize<Dictionary<string, string>>(model.AdditionalHeaders) ?? [];
                    break;
            }

            connection.Put(metadata);
        }
        else
        {
            connection.Alter<StdioMcpConnectionMetadata>(metadata =>
            {
                metadata.Command = model.Command?.Trim();
                metadata.Arguments = string.IsNullOrWhiteSpace(model.Arguments)
                    ? []
                    : JsonSerializer.Deserialize<string[]>(model.Arguments) ?? [];
                metadata.WorkingDirectory = model.WorkingDirectory?.Trim();
                metadata.EnvironmentVariables = string.IsNullOrWhiteSpace(model.EnvironmentVariables)
                    ? []
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(model.EnvironmentVariables) ?? [];
            });
        }
    }

    private static void PopulateOAuthCommon(McpConnectionViewModel model, SseMcpConnectionMetadata metadata)
    {
        metadata.OAuth2TokenEndpoint = model.OAuth2TokenEndpoint?.Trim();
        metadata.OAuth2ClientId = model.OAuth2ClientId?.Trim();
        metadata.OAuth2Scopes = model.OAuth2Scopes?.Trim();
    }

    private static string ProtectOrReuse(string newValue, string existingValue, IDataProtector protector)
        => string.IsNullOrWhiteSpace(newValue) ? existingValue : protector.Protect(newValue);

    private static McpConnectionViewModel ToViewModel(McpConnection connection)
    {
        var model = new McpConnectionViewModel
        {
            ItemId = connection.ItemId,
            DisplayText = connection.DisplayText,
            Source = connection.Source,
        };

        if (connection.Source == McpConstants.TransportTypes.Sse)
        {
            var metadata = connection.As<SseMcpConnectionMetadata>();
            model.Endpoint = metadata.Endpoint?.ToString();
            model.AuthenticationType = metadata.AuthenticationType;
            model.ApiKeyHeaderName = metadata.ApiKeyHeaderName;
            model.ApiKeyPrefix = metadata.ApiKeyPrefix;
            model.HasApiKey = !string.IsNullOrEmpty(metadata.ApiKey);
            model.BasicUsername = metadata.BasicUsername;
            model.HasBasicPassword = !string.IsNullOrEmpty(metadata.BasicPassword);
            model.OAuth2TokenEndpoint = metadata.OAuth2TokenEndpoint;
            model.OAuth2ClientId = metadata.OAuth2ClientId;
            model.OAuth2Scopes = metadata.OAuth2Scopes;
            model.HasOAuth2ClientSecret = !string.IsNullOrEmpty(metadata.OAuth2ClientSecret);
            model.OAuth2KeyId = metadata.OAuth2KeyId;
            model.HasOAuth2PrivateKey = !string.IsNullOrEmpty(metadata.OAuth2PrivateKey);
            model.HasOAuth2ClientCertificate = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificate);
            model.HasOAuth2ClientCertificatePassword = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificatePassword);
            model.AdditionalHeaders = metadata.AdditionalHeaders is not null
                ? JsonSerializer.Serialize(metadata.AdditionalHeaders, _indentedJsonOptions)
                : "{}";
        }
        else
        {
            var metadata = connection.As<StdioMcpConnectionMetadata>();
            model.Command = metadata.Command;
            model.Arguments = metadata.Arguments is { Length: > 0 }
                ? JsonSerializer.Serialize(metadata.Arguments, _indentedJsonOptions)
                : "[]";
            model.WorkingDirectory = metadata.WorkingDirectory;
            model.EnvironmentVariables = metadata.EnvironmentVariables is { Count: > 0 }
                ? JsonSerializer.Serialize(metadata.EnvironmentVariables, _indentedJsonOptions)
                : "{}";
        }

        return model;
    }
}
