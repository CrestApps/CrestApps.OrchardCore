using System.ComponentModel.DataAnnotations;

namespace CrestApps.Core.Mvc.Web.Areas.Mcp.ViewModels;

public sealed class McpResourceViewModel
{
    public string ItemId { get; set; }

    [Required]
    public string DisplayText { get; set; }

    [Required]
    public string Source { get; set; } = "ftp";

    [Required]
    public string Name { get; set; }

    public string Description { get; set; }

    public string MimeType { get; set; }

    public string Path { get; set; }

    public string Host { get; set; }

    public int? Port { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public bool HasPassword { get; set; }

    public string EncryptionMode { get; set; }

    public string DataConnectionType { get; set; }

    public bool ValidateAnyCertificate { get; set; }

    public int? ConnectTimeout { get; set; }

    public int? ReadTimeout { get; set; }

    public int? RetryAttempts { get; set; }

    public string PrivateKey { get; set; }

    public bool HasPrivateKey { get; set; }

    public string Passphrase { get; set; }

    public bool HasPassphrase { get; set; }

    public string ProxyType { get; set; }

    public string ProxyHost { get; set; }

    public int? ProxyPort { get; set; }

    public string ProxyUsername { get; set; }

    public string ProxyPassword { get; set; }

    public bool HasProxyPassword { get; set; }

    public int? ConnectionTimeout { get; set; }

    public int? KeepAliveInterval { get; set; }
}
