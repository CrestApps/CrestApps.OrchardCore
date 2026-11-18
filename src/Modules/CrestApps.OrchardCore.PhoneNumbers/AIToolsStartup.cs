using CrestApps.Core.AI;
using CrestApps.OrchardCore.PhoneNumbers.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Registers the phone number check AI tool whenever the Phone Numbers Services and AI features are on. It is pure
/// computation and needs a signed-in caller only.
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
        services.AddCoreAITool<NormalizePhoneNumberTool>(NormalizePhoneNumberTool.TheName)
            .WithTitle(S["Normalize Phone Number"])
            .WithDescription(S["Parses a phone number for a region and returns whether it is valid, its E.164 form, display form, region and time zones. Call it before storing a phone number so it can match CRM, caller-ID and do-not-call records."])
            .WithCategory(S["Phone Numbers"])
            .Selectable();
    }
}
