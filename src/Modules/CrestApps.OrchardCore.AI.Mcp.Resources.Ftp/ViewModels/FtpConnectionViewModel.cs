namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.ViewModels;

public class FtpConnectionViewModel
{
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
}
