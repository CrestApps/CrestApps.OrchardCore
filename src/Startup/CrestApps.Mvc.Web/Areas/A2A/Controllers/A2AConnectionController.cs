using System.Text.Json;
using CrestApps.AI.A2A;
using CrestApps.AI.A2A.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Mvc.Web.Areas.A2A.Controllers;

[Area("A2A")]
[Authorize(Policy = "Admin")]
public sealed class A2AConnectionController : Controller
{
    private readonly ICatalog<A2AConnection> _catalog;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public A2AConnectionController(
        ICatalog<A2AConnection> catalog,
        IDataProtectionProvider dataProtectionProvider)
    {
        _catalog = catalog;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public async Task<IActionResult> Index()
    {
        var connections = await _catalog.GetAllAsync();

        return View(connections.OrderBy(connection => connection.DisplayText, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public IActionResult Create()
        => View(new A2AConnectionViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(A2AConnectionViewModel model)
    {
        ValidateModel(model, isEditing: false);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var connection = new A2AConnection
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = DateTime.UtcNow,
        };

        ApplyToConnection(model, connection);

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

        return View(A2AConnectionViewModel.FromConnection(connection));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(A2AConnectionViewModel model)
    {
        var connection = await _catalog.FindByIdAsync(model.ItemId);

        if (connection == null)
        {
            return NotFound();
        }

        ValidateModel(model, isEditing: true);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        ApplyToConnection(model, connection);

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

    private void ValidateModel(A2AConnectionViewModel model, bool isEditing)
    {
        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            ModelState.AddModelError(nameof(model.DisplayText), "Display text is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            ModelState.AddModelError(nameof(model.Endpoint), "Endpoint is required.");
        }
        else if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ModelState.AddModelError(nameof(model.Endpoint), "Endpoint must be a valid HTTP or HTTPS URL.");
        }

        switch (model.AuthenticationType)
        {
            case A2AClientAuthenticationType.ApiKey:

                if ((!isEditing || !model.HasApiKey) && string.IsNullOrWhiteSpace(model.ApiKey))
                {
                    ModelState.AddModelError(nameof(model.ApiKey), "API key is required.");
                }

                break;

            case A2AClientAuthenticationType.Basic:

                if (string.IsNullOrWhiteSpace(model.BasicUsername))
                {
                    ModelState.AddModelError(nameof(model.BasicUsername), "Username is required.");
                }

                if ((!isEditing || !model.HasBasicPassword) && string.IsNullOrWhiteSpace(model.BasicPassword))
                {
                    ModelState.AddModelError(nameof(model.BasicPassword), "Password is required.");
                }

                break;

            case A2AClientAuthenticationType.OAuth2ClientCredentials:
                ValidateOAuth2Common(model);

                if ((!isEditing || !model.HasOAuth2ClientSecret) && string.IsNullOrWhiteSpace(model.OAuth2ClientSecret))
                {
                    ModelState.AddModelError(nameof(model.OAuth2ClientSecret), "Client secret is required.");
                }

                break;

            case A2AClientAuthenticationType.OAuth2PrivateKeyJwt:
                ValidateOAuth2Common(model);

                if ((!isEditing || !model.HasOAuth2PrivateKey) && string.IsNullOrWhiteSpace(model.OAuth2PrivateKey))
                {
                    ModelState.AddModelError(nameof(model.OAuth2PrivateKey), "Private key is required.");
                }

                break;

            case A2AClientAuthenticationType.OAuth2Mtls:
                ValidateOAuth2Common(model);

                if ((!isEditing || !model.HasOAuth2ClientCertificate) && string.IsNullOrWhiteSpace(model.OAuth2ClientCertificate))
                {
                    ModelState.AddModelError(nameof(model.OAuth2ClientCertificate), "Client certificate is required.");
                }

                break;

            case A2AClientAuthenticationType.CustomHeaders:

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

    private void ValidateOAuth2Common(A2AConnectionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.OAuth2TokenEndpoint))
        {
            ModelState.AddModelError(nameof(model.OAuth2TokenEndpoint), "Token endpoint is required.");
        }
        else if (!Uri.TryCreate(model.OAuth2TokenEndpoint, UriKind.Absolute, out var _))
        {
            ModelState.AddModelError(nameof(model.OAuth2TokenEndpoint), "Token endpoint must be a valid URL.");
        }

        if (string.IsNullOrWhiteSpace(model.OAuth2ClientId))
        {
            ModelState.AddModelError(nameof(model.OAuth2ClientId), "Client ID is required.");
        }
    }

    private void ApplyToConnection(A2AConnectionViewModel model, A2AConnection connection)
    {
        connection.DisplayText = model.DisplayText?.Trim();
        connection.Endpoint = model.Endpoint?.Trim();

        var metadata = connection.As<A2AConnectionMetadata>();
        var protector = _dataProtectionProvider.CreateProtector(A2AConstants.DataProtectionPurpose);
        var existingApiKey = metadata.ApiKey;
        var existingBasicPassword = metadata.BasicPassword;
        var existingOAuth2ClientSecret = metadata.OAuth2ClientSecret;
        var existingOAuth2PrivateKey = metadata.OAuth2PrivateKey;
        var existingOAuth2ClientCertificate = metadata.OAuth2ClientCertificate;
        var existingOAuth2ClientCertificatePassword = metadata.OAuth2ClientCertificatePassword;

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
            case A2AClientAuthenticationType.ApiKey:
                metadata.ApiKeyHeaderName = model.ApiKeyHeaderName;
                metadata.ApiKeyPrefix = model.ApiKeyPrefix;
                metadata.ApiKey = ProtectOrReuse(model.ApiKey, existingApiKey, protector);
                break;

            case A2AClientAuthenticationType.Basic:
                metadata.BasicUsername = model.BasicUsername;
                metadata.BasicPassword = ProtectOrReuse(model.BasicPassword, existingBasicPassword, protector);
                break;

            case A2AClientAuthenticationType.OAuth2ClientCredentials:
                PopulateOAuth2Common(model, metadata);
                metadata.OAuth2ClientSecret = ProtectOrReuse(model.OAuth2ClientSecret, existingOAuth2ClientSecret, protector);
                break;

            case A2AClientAuthenticationType.OAuth2PrivateKeyJwt:
                PopulateOAuth2Common(model, metadata);
                metadata.OAuth2KeyId = model.OAuth2KeyId;
                metadata.OAuth2PrivateKey = ProtectOrReuse(model.OAuth2PrivateKey, existingOAuth2PrivateKey, protector);
                break;

            case A2AClientAuthenticationType.OAuth2Mtls:
                PopulateOAuth2Common(model, metadata);
                metadata.OAuth2ClientCertificate = ProtectOrReuse(model.OAuth2ClientCertificate, existingOAuth2ClientCertificate, protector);
                metadata.OAuth2ClientCertificatePassword = ProtectOrReuse(model.OAuth2ClientCertificatePassword, existingOAuth2ClientCertificatePassword, protector);
                break;

            case A2AClientAuthenticationType.CustomHeaders:
                metadata.AdditionalHeaders = string.IsNullOrWhiteSpace(model.AdditionalHeaders)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(model.AdditionalHeaders);
                break;
        }

        connection.Put(metadata);
    }

    private static void PopulateOAuth2Common(A2AConnectionViewModel model, A2AConnectionMetadata metadata)
    {
        metadata.OAuth2TokenEndpoint = model.OAuth2TokenEndpoint;
        metadata.OAuth2ClientId = model.OAuth2ClientId;
        metadata.OAuth2Scopes = model.OAuth2Scopes;
    }

    private static string ProtectOrReuse(string value, string existingProtectedValue, IDataProtector protector)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return protector.Protect(value);
        }

        return existingProtectedValue;
    }
}
