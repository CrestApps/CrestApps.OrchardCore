using CrestApps.Core.Services;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Server;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the External Tools core.
/// </summary>
public static class LearningLtiCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the External tool course item kind, the tool store and manager, the line item and score stores, the
    /// platform and its key set on the shared signing keys (the feature also calls <c>AddSigningKeys()</c>), the message
    /// hints, the nonce store, the tool directory and the launch services. The feature depends on Orchard Core Secrets
    /// and the OpenID server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningLtiCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IExternalToolStore, ExternalToolStore>();
        services.TryAddScoped<IExternalToolManager, ExternalToolManager>();
        services.TryAddScoped<ICatalogManager<ExternalTool>>(serviceProvider => serviceProvider.GetRequiredService<IExternalToolManager>());
        services.TryAddScoped<LtiLineItemStore>();
        services.TryAddScoped<LtiScoreStore>();

        services.AddSigningKeySet(LtiConstants.PlatformKeySet, LtiPlatformKeys.Configure);
        services.TryAddScoped<LtiPlatformKeys>();
        services.TryAddScoped<ILtiPlatform, LtiPlatform>();
        services.TryAddSingleton<LtiMessageHints>();
        services.TryAddSingleton<LtiNonceStore>();
        services.TryAddScoped<ILtiToolDirectory, LtiToolDirectory>();
        services.TryAddScoped<ILtiLaunchContextFactory, LtiLaunchContextFactory>();
        services.TryAddScoped<LtiAuthorizationService>();
        services.TryAddSingleton<LtiDeepLinkingDataTokens>();
        services.TryAddScoped<LtiDeepLinkingService>();
        services.TryAddScoped<LtiCourseMembers>();
        services.TryAddScoped<LtiAgsService>();
        services.TryAddScoped<LtiServiceAccess>();

        // LTI tools' client assertions at the OpenID server's token endpoint (see LtiClientAssertionHandler).
        services.Configure<OpenIddictServerOptions>(options => options.Handlers.Add(LtiClientAssertionHandler.Descriptor));
        services.TryAdd(LtiClientAssertionHandler.Descriptor.ServiceDescriptor);

        services.AddCourseItemKind<ExternalToolItemPart>(LtiConstants.ExternalToolKind, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("External tool", typeof(LearningLtiCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Activity", typeof(LearningLtiCoreServiceCollectionExtensions));
            kind.ContentType = LtiConstants.ContentTypes.ExternalToolItem;

            // Graded first: the tool's server posts the score, so it is not marked as reported by the browser.
            kind.CompletionModes.Add(CompletionModes.Graded);
            kind.CompletionModes.Add(CompletionModes.Passed);
            kind.CompletionModes.Add(CompletionModes.Viewed);
            kind.CompletionModes.Add(CompletionModes.MarkedDone);
            kind.ProducesGrade = true;
            kind.Icon = "fa-solid fa-plug";
        });

        services.AddHttpClient(LtiToolDirectory.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));

        return services;
    }
}
