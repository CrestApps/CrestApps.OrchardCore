using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

public class AgentFeedSettingsViewModel
{
    /// <summary>
    /// Gets or sets the two-letter codes of the target countries, separated by commas.
    /// </summary>
    public string TargetCountries { get; set; }

    public bool SftpEnabled { get; set; }

    public string SftpHost { get; set; }

    public int SftpPort { get; set; } = 22;

    public string SftpUsername { get; set; }

    /// <summary>
    /// Gets or sets a new password, or none to keep the one stored.
    /// </summary>
    public string SftpPassword { get; set; }

    public bool RemoveSftpPassword { get; set; }

    /// <summary>
    /// Gets or sets a new private key, or none to keep the one stored.
    /// </summary>
    public string SftpPrivateKey { get; set; }

    public bool RemoveSftpPrivateKey { get; set; }

    /// <summary>
    /// Gets or sets a new passphrase of the private key, or none to keep the one stored.
    /// </summary>
    public string SftpPrivateKeyPassphrase { get; set; }

    public string SftpHostKeyFingerprint { get; set; }

    public string SftpRemoteFolder { get; set; } = "/";

    public string SftpFormat { get; set; } = OpenAIProductFeedFormats.JsonLinesGzip;

    public string SftpCurrency { get; set; }

    public List<AcpFeedTargetViewModel> FeedApiTargets { get; set; } = [];

    [BindNever]
    public bool HasSftpPassword { get; set; }

    [BindNever]
    public bool HasSftpPrivateKey { get; set; }

    [BindNever]
    public bool HasSftpPrivateKeyPassphrase { get; set; }

    [BindNever]
    public bool HasBaseUrl { get; set; }

    [BindNever]
    public string StoreCountry { get; set; }

    [BindNever]
    public string JsonLinesUrl { get; set; }

    [BindNever]
    public string CsvUrl { get; set; }

    [BindNever]
    public OpenAIProductFeed Feed { get; set; }

    [BindNever]
    public AgentFeedStatusDocument Status { get; set; }
}
