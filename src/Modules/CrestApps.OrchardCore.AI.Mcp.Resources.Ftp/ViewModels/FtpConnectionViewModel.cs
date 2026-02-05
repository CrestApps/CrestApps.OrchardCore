namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.ViewModels;

public class FtpConnectionViewModel
{
    public string Host { get; set; }

    public int? Port { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public bool HasPassword { get; set; }

    public bool UseSsl { get; set; }
}
