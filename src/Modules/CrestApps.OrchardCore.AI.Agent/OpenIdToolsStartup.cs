using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.OpenId;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, read and delete OpenID Connect client applications and that list, save and delete
/// OpenID Connect scopes, when the OpenID Connect Management feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.OpenIdManagement)]
public sealed class OpenIdToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpenIdToolsStartup(IStringLocalizer<OpenIdToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListOpenIdApplicationsTool>(ListOpenIdApplicationsTool.TheName)
            .WithTitle(S["List OpenID Applications"])
            .WithDescription(S["Lists the OpenID Connect client applications registered on this site with id, client id, display name and type; optional search by display name and paging."])
            .WithCategory(S["OpenID Connect"])
            .Selectable();

        services.AddCoreAITool<GetOpenIdApplicationTool>(GetOpenIdApplicationTool.TheName)
            .WithTitle(S["Get OpenID Application"])
            .WithDescription(S["Gets one OpenID application by id or client id: type, consent type, redirect URIs, allowed flows and endpoints, PKCE and PAR requirements, roles and scopes. It never returns the client secret."])
            .WithCategory(S["OpenID Connect"])
            .Selectable();

        services.AddCoreAITool<DeleteOpenIdApplicationTool>(DeleteOpenIdApplicationTool.TheName)
            .WithTitle(S["Delete OpenID Application"])
            .WithDescription(S["Deletes an OpenID application; every client that uses its client id stops working."])
            .WithCategory(S["OpenID Connect"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListOpenIdScopesTool>(ListOpenIdScopesTool.TheName)
            .WithTitle(S["List OpenID Scopes"])
            .WithDescription(S["Lists the OpenID scopes with name, display name, description and resources."])
            .WithCategory(S["OpenID Connect"])
            .Selectable();

        services.AddCoreAITool<SaveOpenIdScopeTool>(SaveOpenIdScopeTool.TheName)
            .WithTitle(S["Save OpenID Scope"])
            .WithDescription(S["Creates or updates an OpenID scope: name, display name, description and the resources (audiences) it grants."])
            .WithCategory(S["OpenID Connect"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteOpenIdScopeTool>(DeleteOpenIdScopeTool.TheName)
            .WithTitle(S["Delete OpenID Scope"])
            .WithDescription(S["Deletes an OpenID scope; applications that request it will be refused."])
            .WithCategory(S["OpenID Connect"])
            .RequireApproval()
            .Selectable();
    }
}
