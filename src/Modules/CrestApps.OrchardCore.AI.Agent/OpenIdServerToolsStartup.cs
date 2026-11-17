using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.OpenId;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that creates or changes an OpenID Connect client application, when the OpenID Connect
/// Management and Authorization Server features are on. The tool saves through the application recipe step of the
/// OpenID module, which the Authorization Server feature registers.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.OpenIdManagement, OrchardCoreFeatureIds.OpenIdServer)]
public sealed class OpenIdServerToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdServerToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpenIdServerToolsStartup(IStringLocalizer<OpenIdServerToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SaveOpenIdApplicationTool>(SaveOpenIdApplicationTool.TheName)
            .WithTitle(S["Save OpenID Application"])
            .WithDescription(S["Creates a public client application or updates an existing application's display name, redirect URIs, flows, endpoints, requirements, roles and scopes. It cannot set or rotate a client secret, so creating a confidential client or turning a public client into a confidential one must be done on the Applications page."])
            .WithCategory(S["OpenID Connect"])
            .RequireApproval()
            .Selectable();
    }
}
