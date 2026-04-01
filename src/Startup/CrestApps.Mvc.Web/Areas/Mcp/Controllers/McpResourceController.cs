using CrestApps.AI.Mcp;
using CrestApps.AI.Mcp.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace CrestApps.Mvc.Web.Areas.Mcp.Controllers;

[Area("Mcp")]
[Authorize(Policy = "Admin")]
public sealed class McpResourceController : Controller
{
    private readonly ISourceCatalog<McpResource> _catalog;
    private readonly McpOptions _mcpOptions;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly TimeProvider _timeProvider;

    public McpResourceController(
        ISourceCatalog<McpResource> catalog,
        IOptions<McpOptions> mcpOptions,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider)
    {
        _catalog = catalog;
        _mcpOptions = mcpOptions.Value;
        _dataProtectionProvider = dataProtectionProvider;
        _timeProvider = timeProvider;
    }

    public async Task<IActionResult> Index()
        => View((await _catalog.GetAllAsync())
        .OrderBy(resource => resource.DisplayText, StringComparer.OrdinalIgnoreCase)
        .ToList());

    public IActionResult Create()
    {
        PopulateResourceTypes();

        return View(new McpResourceViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(McpResourceViewModel model)
    {
        Validate(model, false);

        if (!ModelState.IsValid)
        {
            PopulateResourceTypes();

            return View(model);
        }

        var resource = new McpResource
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime,
        };

        Apply(model, resource);

        await _catalog.CreateAsync(resource);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var resource = await _catalog.FindByIdAsync(id);

        if (resource == null)
        {
            return NotFound();
        }

        PopulateResourceTypes();

        return View(ToViewModel(resource));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(McpResourceViewModel model)
    {
        var resource = await _catalog.FindByIdAsync(model.ItemId);

        if (resource == null)
        {
            return NotFound();
        }

        Validate(model, true);

        if (!ModelState.IsValid)
        {
            PopulateResourceTypes();

            return View(model);
        }

        Apply(model, resource);

        await _catalog.UpdateAsync(resource);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var resource = await _catalog.FindByIdAsync(id);

        if (resource == null)
        {
            return NotFound();
        }

        await _catalog.DeleteAsync(resource);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private void PopulateResourceTypes()
        => ViewBag.ResourceTypes = _mcpOptions.ResourceTypes
        .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private void Validate(McpResourceViewModel model, bool isEditing)
    {
        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            ModelState.AddModelError(nameof(model.DisplayText), "Display text is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (!_mcpOptions.ResourceTypes.ContainsKey(model.Source))
        {
            ModelState.AddModelError(nameof(model.Source), "Resource type is not supported.");
        }

        switch (model.Source)
        {
            case FtpResourceConstants.Type:

                if (string.IsNullOrWhiteSpace(model.Host))
                {
                    ModelState.AddModelError(nameof(model.Host), "Host is required.");
                }

                if ((!isEditing || !model.HasPassword) &&
                    string.IsNullOrWhiteSpace(model.Password) &&
                        !string.IsNullOrWhiteSpace(model.Username))
                {
                    ModelState.AddModelError(nameof(model.Password), "Password is required when a username is provided.");
                }

                break;

            case SftpResourceConstants.Type:

                if (string.IsNullOrWhiteSpace(model.Host))
                {
                    ModelState.AddModelError(nameof(model.Host), "Host is required.");
                }

                if ((!isEditing || !model.HasPassword) &&
                    (!isEditing || !model.HasPrivateKey) &&
                        string.IsNullOrWhiteSpace(model.Password) &&
                            string.IsNullOrWhiteSpace(model.PrivateKey))
                {
                    ModelState.AddModelError(nameof(model.Password), "Provide a password or private key.");
                }

                break;
        }
    }

    private void Apply(McpResourceViewModel model, McpResource resource)
    {
        resource.DisplayText = model.DisplayText.Trim();
        resource.Source = model.Source;
        resource.Resource = new Resource
        {
            Uri = BuildUri(model.Source, resource.ItemId, model.Path?.Trim()),
            Name = model.Name.Trim(),
            Title = model.DisplayText.Trim(),
            Description = model.Description?.Trim(),
            MimeType = model.MimeType?.Trim(),
        };

        if (model.Source == FtpResourceConstants.Type)
        {
            var protector = _dataProtectionProvider.CreateProtector(FtpResourceConstants.DataProtectionPurpose);
            var metadata = resource.As<FtpConnectionMetadata>();
            var existingPassword = metadata.Password;
            metadata.Host = model.Host?.Trim();
            metadata.Port = model.Port;
            metadata.Username = model.Username?.Trim();
            metadata.Password = ProtectOrReuse(model.Password, existingPassword, protector);
            metadata.EncryptionMode = model.EncryptionMode?.Trim();
            metadata.DataConnectionType = model.DataConnectionType?.Trim();
            metadata.ValidateAnyCertificate = model.ValidateAnyCertificate;
            metadata.ConnectTimeout = model.ConnectTimeout;
            metadata.ReadTimeout = model.ReadTimeout;
            metadata.RetryAttempts = model.RetryAttempts;
            resource.Put(metadata);
        }
        else if (model.Source == SftpResourceConstants.Type)
        {
            var protector = _dataProtectionProvider.CreateProtector(SftpResourceConstants.DataProtectionPurpose);
            var metadata = resource.As<SftpConnectionMetadata>();
            var existingPassword = metadata.Password;
            var existingPrivateKey = metadata.PrivateKey;
            var existingPassphrase = metadata.Passphrase;
            var existingProxyPassword = metadata.ProxyPassword;
            metadata.Host = model.Host?.Trim();
            metadata.Port = model.Port;
            metadata.Username = model.Username?.Trim();
            metadata.Password = ProtectOrReuse(model.Password, existingPassword, protector);
            metadata.PrivateKey = ProtectOrReuse(model.PrivateKey, existingPrivateKey, protector);
            metadata.Passphrase = ProtectOrReuse(model.Passphrase, existingPassphrase, protector);
            metadata.ProxyType = model.ProxyType?.Trim();
            metadata.ProxyHost = model.ProxyHost?.Trim();
            metadata.ProxyPort = model.ProxyPort;
            metadata.ProxyUsername = model.ProxyUsername?.Trim();
            metadata.ProxyPassword = ProtectOrReuse(model.ProxyPassword, existingProxyPassword, protector);
            metadata.ConnectionTimeout = model.ConnectionTimeout;
            metadata.KeepAliveInterval = model.KeepAliveInterval;
            resource.Put(metadata);
        }
    }

    private static string ProtectOrReuse(string newValue, string existingValue, IDataProtector protector)
        => string.IsNullOrWhiteSpace(newValue) ? existingValue : protector.Protect(newValue);

    private static McpResourceViewModel ToViewModel(McpResource resource)
    {
        var model = new McpResourceViewModel
        {
            ItemId = resource.ItemId,
            DisplayText = resource.DisplayText,
            Source = resource.Source,
            Name = resource.Resource?.Name,
            Description = resource.Resource?.Description,
            MimeType = resource.Resource?.MimeType,
            Path = ExtractPath(resource.Resource?.Uri, resource.Source, resource.ItemId),
        };

        if (resource.Source == FtpResourceConstants.Type)
        {
            var metadata = resource.As<FtpConnectionMetadata>();
            model.Host = metadata.Host;
            model.Port = metadata.Port;
            model.Username = metadata.Username;
            model.HasPassword = !string.IsNullOrEmpty(metadata.Password);
            model.EncryptionMode = metadata.EncryptionMode;
            model.DataConnectionType = metadata.DataConnectionType;
            model.ValidateAnyCertificate = metadata.ValidateAnyCertificate;
            model.ConnectTimeout = metadata.ConnectTimeout;
            model.ReadTimeout = metadata.ReadTimeout;
            model.RetryAttempts = metadata.RetryAttempts;
        }
        else if (resource.Source == SftpResourceConstants.Type)
        {
            var metadata = resource.As<SftpConnectionMetadata>();
            model.Host = metadata.Host;
            model.Port = metadata.Port;
            model.Username = metadata.Username;
            model.HasPassword = !string.IsNullOrEmpty(metadata.Password);
            model.HasPrivateKey = !string.IsNullOrEmpty(metadata.PrivateKey);
            model.HasPassphrase = !string.IsNullOrEmpty(metadata.Passphrase);
            model.ProxyType = metadata.ProxyType;
            model.ProxyHost = metadata.ProxyHost;
            model.ProxyPort = metadata.ProxyPort;
            model.ProxyUsername = metadata.ProxyUsername;
            model.HasProxyPassword = !string.IsNullOrEmpty(metadata.ProxyPassword);
            model.ConnectionTimeout = metadata.ConnectionTimeout;
            model.KeepAliveInterval = metadata.KeepAliveInterval;
        }

        return model;
    }

    private static string BuildUri(string source, string itemId, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return $"{source}://{itemId}";
        }

        return $"{source}://{itemId}/{path.TrimStart('/')}";
    }

    private static string ExtractPath(string uri, string source, string itemId)
    {
        var prefix = $"{source}://{itemId}";

        if (string.IsNullOrEmpty(uri) || !uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        return uri[prefix.Length..].TrimStart('/');
    }
}
