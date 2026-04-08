namespace CrestApps.Core.AI.Sftp.Models;

public sealed class SftpConnectionMetadata
{
    public string Host { get; set; }

    public int? Port { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public string PrivateKey { get; set; }

    public string Passphrase { get; set; }

    public string ProxyType { get; set; }

    public string ProxyHost { get; set; }

    public int? ProxyPort { get; set; }

    public string ProxyUsername { get; set; }

    public string ProxyPassword { get; set; }

    public int? ConnectionTimeout { get; set; }

    public int? KeepAliveInterval { get; set; }
}
