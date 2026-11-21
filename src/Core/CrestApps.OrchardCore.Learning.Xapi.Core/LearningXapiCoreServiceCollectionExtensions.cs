using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Learning Record Store core.
/// </summary>
public static class LearningXapiCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the statement and document stores, the resources, the request handler, the credentials and launch
    /// tokens, and the protected file area of attachments.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningXapiCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<XapiOptions>();
        services.AddProtectedFileArea<XapiAttachmentFiles>(XapiConstants.AttachmentsFolder);
        services.TryAddScoped<XapiStatementStore>();
        services.TryAddScoped<XapiStatementService>();
        services.TryAddScoped<XapiStatementsResource>();
        services.TryAddScoped<XapiDocumentStore>();
        services.TryAddScoped<XapiActivityStore>();
        services.TryAddScoped<XapiDocumentsResource>();
        services.TryAddScoped<XapiEndpointHandler>();
        services.TryAddScoped<XapiHomePage>();
        services.TryAddScoped<XapiLaunchTokens>();
        services.TryAddScoped<XapiAuthenticator>();

        return services;
    }
}
