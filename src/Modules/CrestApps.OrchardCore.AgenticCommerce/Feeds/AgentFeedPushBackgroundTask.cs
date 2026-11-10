using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Pushes the full product catalog to AI agent platforms once a day: the OpenAI feed snapshot over SFTP when it is set
/// up, and every product to each enabled ACP feed API.
/// </summary>
[BackgroundTask(
    Title = "Agent product feeds push",
    Schedule = "30 2 * * *",
    Description = "Pushes the daily product snapshot to AI agent platforms over SFTP and through ACP feed APIs.",
    LockTimeout = 5_000,
    LockExpiration = 3_600_000)]
public sealed class AgentFeedPushBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var siteService = serviceProvider.GetRequiredService<ISiteService>();
        var logger = serviceProvider.GetRequiredService<ILogger<AgentFeedPushBackgroundTask>>();
        var settings = await siteService.GetSettingsAsync<AgentFeedSettings>();

        try
        {
            if (settings.Sftp?.Enabled == true)
            {
                await serviceProvider.GetRequiredService<AgentFeedSftpPusher>().PushAsync(cancellationToken);
            }

            var acp = serviceProvider.GetRequiredService<AcpFeedPusher>();

            if (await acp.HasTargetsAsync())
            {
                await acp.PushAllAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The daily push of the agent product feeds failed.");
        }
    }
}
