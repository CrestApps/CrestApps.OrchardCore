using CrestApps.Core.AI;
using CrestApps.OrchardCore.Addresses.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Registers the address check AI tool whenever the Addresses and AI features are on. It needs a signed-in caller and no
/// permission, as the address format endpoint the editor reads is public.
/// </summary>
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

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ValidateAddressTool>(ValidateAddressTool.TheName)
            .WithTitle(S["Validate Address"])
            .WithDescription(S["Checks a postal address against its country's rules and returns the problems per field, the normalized address, the lines as printed, and which fields that country uses and requires. Call it before saving an address on a customer, account location, staff member or content item."])
            .WithCategory(S["Addresses"])
            .Selectable();
    }
}
