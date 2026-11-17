using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save, delete and move secrets, when the Secrets feature is on. No tool returns or
/// takes the value of a secret.
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

        services.AddCoreAITool<SaveSecretTool>(SaveSecretTool.TheName)
            .WithTitle(S["Save Secret"])
            .WithDescription(S["Creates an X.509 secret that points to a certificate in the server's certificate store, or updates any secret's description and expiry date. It cannot set a text value, a PEM certificate or a private key, and it does not create RSA key secrets; a person must enter or create those on the Secrets page."])
            .WithCategory(S["Secrets"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteSecretTool>(DeleteSecretTool.TheName)
            .WithTitle(S["Delete Secret"])
            .WithDescription(S["Deletes a secret from its store; this cannot be undone, and every setting that references the secret by name stops working."])
            .WithCategory(S["Secrets"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<MoveSecretsTool>(MoveSecretsTool.TheName)
            .WithTitle(S["Move Secrets"])
            .WithDescription(S["Moves one secret, or all secrets, from one writable secret store to another, for example from the database store to Azure Key Vault, and reports the result per secret."])
            .WithCategory(S["Secrets"])
            .RequireApproval()
            .Selectable();
    }
}
