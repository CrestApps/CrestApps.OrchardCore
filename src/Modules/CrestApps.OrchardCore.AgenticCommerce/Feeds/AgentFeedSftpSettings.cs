namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Where the daily snapshot of the OpenAI product feed is pushed over SFTP, and how to sign in there.
/// </summary>
public sealed class AgentFeedSftpSettings
{
    /// <summary>
    /// Gets or sets whether the snapshot is pushed every day.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the SFTP server's host name.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the SFTP server's port.
    /// </summary>
    public int Port { get; set; } = 22;

    /// <summary>
    /// Gets or sets the user name to sign in with.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the password, protected with <see cref="AgentFeedSettings.ProtectionPurpose"/>, or none.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the private key in OpenSSH or PEM form, protected with
    /// <see cref="AgentFeedSettings.ProtectionPurpose"/>, or none.
    /// </summary>
    public string PrivateKey { get; set; }

    /// <summary>
    /// Gets or sets the private key's passphrase, protected with <see cref="AgentFeedSettings.ProtectionPurpose"/>, or
    /// none.
    /// </summary>
    public string PrivateKeyPassphrase { get; set; }

    /// <summary>
    /// Gets or sets the server's SHA-256 host key fingerprint the push checks before it signs in, or none to accept any.
    /// </summary>
    public string HostKeyFingerprint { get; set; }

    /// <summary>
    /// Gets or sets the folder on the server the files go in.
    /// </summary>
    public string RemoteFolder { get; set; } = "/";

    /// <summary>
    /// Gets or sets the file format: <c>jsonl.gz</c> or <c>csv.gz</c>.
    /// </summary>
    public string Format { get; set; } = OpenAIProductFeedFormats.JsonLinesGzip;

    /// <summary>
    /// Gets or sets the ISO 4217 code of the currency the feed is priced in, or none for each product's own.
    /// </summary>
    public string Currency { get; set; }
}
