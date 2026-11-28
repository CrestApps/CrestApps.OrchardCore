using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Pushes the agent product feed now with the saved settings: over SFTP when the SFTP push is on, and to every enabled
/// ACP feed API target. The Agent feeds settings page's "Push now" button and the <c>pushAgentFeeds</c> AI tool both
/// call it.
/// </summary>
public sealed class AgentFeedPushService
{
    private readonly ISiteService _siteService;
    private readonly AgentFeedSftpPusher _sftpPusher;
    private readonly AcpFeedPusher _acpPusher;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentFeedPushService"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="sftpPusher">The SFTP pusher.</param>
    /// <param name="acpPusher">The ACP feed API pusher.</param>
    public AgentFeedPushService(ISiteService siteService, AgentFeedSftpPusher sftpPusher, AcpFeedPusher acpPusher)
    {
        _siteService = siteService;
        _sftpPusher = sftpPusher;
        _acpPusher = acpPusher;
    }

    /// <summary>
    /// Pushes the feed now.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What was pushed and how each push went; nothing when no push is set up.</returns>
    public async Task<AgentFeedPushRun> PushAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<AgentFeedSettings>();
        AgentFeedPushResult sftp = null;
        var targets = new List<AgentFeedTargetPush>();
        var hasTargets = false;

        if (settings.Sftp?.Enabled == true)
        {
            sftp = await _sftpPusher.PushAsync(cancellationToken);
        }

        if (await _acpPusher.HasTargetsAsync())
        {
            hasTargets = true;

            var names = (settings.FeedApiTargets ?? []).ToDictionary(target => target.Id ?? string.Empty, target => target.Name, StringComparer.Ordinal);

            foreach (var (targetId, result) in await _acpPusher.PushAllAsync(cancellationToken))
            {
                targets.Add(new AgentFeedTargetPush(targetId, names.GetValueOrDefault(targetId) ?? targetId, result));
            }
        }

        return new AgentFeedPushRun(sftp, hasTargets, targets);
    }
}

/// <summary>
/// What a push of the agent product feed did.
/// </summary>
/// <param name="Sftp">The SFTP push's result, or <see langword="null"/> when the SFTP push is off.</param>
/// <param name="HasTargets">Whether an enabled ACP feed API target was pushed to.</param>
/// <param name="Targets">The result of each feed API target.</param>
public sealed record AgentFeedPushRun(AgentFeedPushResult Sftp, bool HasTargets, IReadOnlyList<AgentFeedTargetPush> Targets)
{
    /// <summary>
    /// Gets whether anything was pushed; nothing is when neither the SFTP push nor a feed API is set up.
    /// </summary>
    public bool Pushed
        => Sftp is not null || HasTargets;
}

/// <summary>
/// The result of a push to one ACP feed API target.
/// </summary>
/// <param name="TargetId">The target's id.</param>
/// <param name="Name">The target's name, or its id when it has none.</param>
/// <param name="Result">The push's result.</param>
public sealed record AgentFeedTargetPush(string TargetId, string Name, AgentFeedPushResult Result);
