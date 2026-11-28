using CrestApps.Core.AI;
using CrestApps.OrchardCore.Downloads.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Downloads;

/// <summary>
/// Registers the digital download AI tools when the AI feature is on: an order's download entitlements and revoking or
/// resetting one, with the permission of the order page's Downloads card.
/// </summary>
[Feature(DownloadsConstants.Feature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListOrderDownloadsTool>(ListOrderDownloadsTool.TheName)
            .WithTitle(S["List Order Downloads"])
            .WithDescription(S["Lists the download entitlements of an order: file, downloads used and allowed, expiry, and whether the link is working, revoked, expired or over its limit. Use it when a buyer says a download link does not work."])
            .WithCategory(S["Digital Downloads"])
            .Selectable();

        services.AddCoreAITool<SetOrderDownloadAccessTool>(SetOrderDownloadAccessTool.TheName)
            .WithTitle(S["Set Order Download Access"])
            .WithDescription(S["Revokes one download entitlement so the buyer can no longer download the file, or resets it so the buyer can download again with the count at zero and a new expiry. Takes the entitlement id from listOrderDownloads."])
            .WithCategory(S["Digital Downloads"])
            .Selectable();
    }
}
