using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list and mark the notifications of the signed-in caller, when the Notifications
/// feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NotificationsToolsStartup(IStringLocalizer<NotificationsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListNotificationsTool>(ListNotificationsTool.TheName)
            .WithTitle(S["List Notifications"])
            .WithDescription(S["Lists the calling user's own notifications, newest first, with optional filters for read or unread, type and text, and paging."])
            .WithCategory(S["Communications"])
            .Selectable();

        services.AddCoreAITool<MarkNotificationsTool>(MarkNotificationsTool.TheName)
            .WithTitle(S["Mark Notifications"])
            .WithDescription(S["Marks the calling user's notifications as read or unread, either the given ids or all of them."])
            .WithCategory(S["Communications"])
            .Selectable();
    }
}
