using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Registers agent product feeds: the OpenAI feed at <c>feeds/openai/products.*</c>, its daily SFTP push, the ACP feed
/// API client and its pushes after edits, and Settings › Commerce › Agent feeds.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Feeds)]
public sealed class FeedsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<OpenAIProductFeedService>();
        services.AddScoped<AgentFeedSftpPusher>();
        services.AddScoped<AcpFeedApiClient>();
        services.AddScoped<AcpFeedPusher>();
        services.AddScoped<AgentFeedChangeTracker>();
        services.AddEgressGuardedHttpClient(AcpFeedApiClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddScoped<IContentHandler, AgentFeedProductHandler>();
        services.AddSingleton<IBackgroundTask, AgentFeedPushBackgroundTask>();
        services.AddSiteDisplayDriver<AgentFeedSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(AgentFeedSettingsDisplayDriver.GroupId, AgentFeedPermissions.ManageAgentFeeds);
        services.AddPermissionProvider<AgentFeedPermissionProvider>();
        services.AddNavigationProvider<AgentFeedsAdminMenu>();
    }
}
