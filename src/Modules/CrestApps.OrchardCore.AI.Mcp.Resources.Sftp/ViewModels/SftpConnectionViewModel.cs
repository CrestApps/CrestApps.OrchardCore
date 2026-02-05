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
}
