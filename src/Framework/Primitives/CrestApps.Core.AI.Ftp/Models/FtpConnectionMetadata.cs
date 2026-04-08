namespace CrestApps.Core.AI.Ftp.Models;

public sealed class FtpConnectionMetadata
{
    public string Host { get; set; }

    public int? Port { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public string EncryptionMode { get; set; }

    public string DataConnectionType { get; set; }

    public bool ValidateAnyCertificate { get; set; }

    public int? ConnectTimeout { get; set; }

    public int? ReadTimeout { get; set; }

    public int? RetryAttempts { get; set; }
}
