using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Documents;
using OrchardCore.Modules;
using OrchardCore.Settings;
using Renci.SshNet;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Pushes the full OpenAI product feed over SFTP, as OpenAI takes it: the whole catalog in shards of at most 500,000
/// items with stable names, overwritten on each push, and the shards a smaller catalog no longer needs deleted.
/// </summary>
public sealed partial class AgentFeedSftpPusher
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly ISiteService _siteService;
    private readonly OpenAIProductFeedService _feeds;
    private readonly IDocumentManager<AgentFeedStatusDocument> _statusManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentFeedSftpPusher"/> class.
    /// </summary>
    /// <param name="siteService">The site service, for the agent feed settings.</param>
    /// <param name="feeds">The OpenAI product feed.</param>
    /// <param name="statusManager">The document the last push is recorded in.</param>
    /// <param name="dataProtectionProvider">The tenant's data protection, for the stored password and key.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AgentFeedSftpPusher(
        ISiteService siteService,
        OpenAIProductFeedService feeds,
        IDocumentManager<AgentFeedStatusDocument> statusManager,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<AgentFeedSftpPusher> logger)
    {
        _siteService = siteService;
        _feeds = feeds;
        _statusManager = statusManager;
        _dataProtectionProvider = dataProtectionProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Pushes the feed when the SFTP push is set up, records how it went, and returns it.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the push.</param>
    public async Task<AgentFeedPushResult> PushAsync(CancellationToken cancellationToken = default)
    {
        var settings = (await _siteService.GetSettingsAsync<AgentFeedSettings>()).Sftp ?? new AgentFeedSftpSettings();
        var result = new AgentFeedPushResult { StartedUtc = _clock.UtcNow };

        try
        {
            if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Username))
            {
                result.Message = "The SFTP host and user name aren't set.";

                return result;
            }

            var format = OpenAIProductFeedFormats.IsGzip(settings.Format) ? settings.Format : OpenAIProductFeedFormats.JsonLinesGzip;
            var feed = await _feeds.GetAsync(settings.Currency, fresh: true, cancellationToken);
            var shards = OpenAIProductFeedWriter.Shard(feed.Records, format);
            var folder = string.IsNullOrWhiteSpace(settings.RemoteFolder) ? "/" : settings.RemoteFolder.Trim().TrimEnd('/') + "/";

            using var client = new SftpClient(CreateConnectionInfo(settings));

            client.HostKeyReceived += (_, e) =>
            {
                var expected = settings.HostKeyFingerprint?.Trim();

                e.CanTrust = string.IsNullOrEmpty(expected) ||
                    string.Equals(NormalizeFingerprint(e.FingerPrintSHA256), NormalizeFingerprint(expected), StringComparison.Ordinal);
            };

            await client.ConnectAsync(cancellationToken);

            if (folder != "/" && !await client.ExistsAsync(folder, cancellationToken))
            {
                await client.CreateDirectoryAsync(folder, cancellationToken);
            }

            foreach (var (fileName, records) in shards)
            {
                using var content = new MemoryStream();

                OpenAIProductFeedWriter.Write(records, format, content);
                content.Position = 0;

                await client.UploadFileAsync(content, folder + fileName, cancellationToken);
                result.Files.Add(fileName);
            }

            // A shard the catalog no longer fills, or a file of the other format, would be read as products still sold.
            var keep = shards.Select(shard => shard.FileName).ToHashSet(StringComparer.Ordinal);

            await foreach (var file in client.ListDirectoryAsync(folder, cancellationToken))
            {
                if (file.IsRegularFile && ShardFilePattern().IsMatch(file.Name) && !keep.Contains(file.Name))
                {
                    await client.DeleteFileAsync(folder + file.Name, cancellationToken);
                }
            }

            client.Disconnect();

            result.ItemCount = feed.Records.Count;
            result.Succeeded = true;
            result.Message = $"{feed.Records.Count} items in {shards.Count} files.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The agent product feed couldn't be pushed over SFTP.");
            result.Message = exception.Message;
        }
        finally
        {
            result.FinishedUtc = _clock.UtcNow;

            var status = await _statusManager.GetOrCreateMutableAsync();

            status.LastSftpPush = result;

            await _statusManager.UpdateAsync(status);
        }

        return result;
    }

    private ConnectionInfo CreateConnectionInfo(AgentFeedSftpSettings settings)
    {
        var username = settings.Username.Trim();
        var methods = new List<AuthenticationMethod>();
        var privateKey = _dataProtectionProvider.UnprotectSettingOrDefault(AgentFeedSettings.ProtectionPurpose, settings.PrivateKey, _logger, "SFTP private key");
        var password = _dataProtectionProvider.UnprotectSettingOrDefault(AgentFeedSettings.ProtectionPurpose, settings.Password, _logger, "SFTP password");

        if (!string.IsNullOrEmpty(privateKey))
        {
            var passphrase = _dataProtectionProvider.UnprotectSettingOrDefault(AgentFeedSettings.ProtectionPurpose, settings.PrivateKeyPassphrase, _logger, "SFTP key passphrase");
            using var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(privateKey));
            var keyFile = string.IsNullOrEmpty(passphrase) ? new PrivateKeyFile(keyStream) : new PrivateKeyFile(keyStream, passphrase);

            methods.Add(new PrivateKeyAuthenticationMethod(username, keyFile));
        }

        if (!string.IsNullOrEmpty(password))
        {
            methods.Add(new PasswordAuthenticationMethod(username, password));
        }

        if (methods.Count == 0)
        {
            throw new InvalidOperationException("Enter the SFTP password or private key.");
        }

        return new ConnectionInfo(settings.Host.Trim(), settings.Port is > 0 and <= 65535 ? settings.Port : 22, username, [.. methods])
        {
            Timeout = _timeout,
        };
    }

    // "SHA256:abc=" and "abc" name the same key; the prefix and the base64 padding are left out.
    private static string NormalizeFingerprint(string fingerprint)
    {
        var value = fingerprint?.Trim() ?? string.Empty;

        if (value.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring("SHA256:".Length);
        }

        return value.TrimEnd('=');
    }

    [GeneratedRegex(@"^products-\d{5}\.(jsonl|csv)\.gz$")]
    private static partial Regex ShardFilePattern();
}
