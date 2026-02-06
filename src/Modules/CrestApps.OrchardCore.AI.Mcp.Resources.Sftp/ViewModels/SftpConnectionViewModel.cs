namespace CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.ViewModels;

public class SftpConnectionViewModel
{
    public string Host { get; set; }

    public int? Port { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public bool HasPassword { get; set; }

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
