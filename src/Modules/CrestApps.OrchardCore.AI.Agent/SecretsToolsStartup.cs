using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that lists secrets, when the Secrets feature is on. It never returns the value of a secret.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Secrets)]
public sealed class SecretsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SecretsToolsStartup(IStringLocalizer<SecretsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListSecretsTool>(ListSecretsTool.TheName)
            .WithTitle(S["List Secrets"])
            .WithDescription(S["Lists secrets by name, store, type, description and created, updated and expiry dates, with optional search, type and expiry filters, plus the available secret stores. It never returns a secret value; use it to find a secret name to reference in a settings page or to spot expiring secrets."])
            .WithCategory(S["Secrets"])
            .Selectable();
    }
}
