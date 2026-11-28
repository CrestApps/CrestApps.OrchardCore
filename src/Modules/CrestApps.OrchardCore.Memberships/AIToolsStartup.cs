using CrestApps.Core.AI;
using CrestApps.OrchardCore.Memberships.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Memberships;

/// <summary>
/// Registers the membership AI tools when the AI feature is on: listing memberships and ending one, as Commerce ›
/// Memberships does, with the same permission.
/// </summary>
[Feature(MembershipsConstants.Feature)]
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
        services.AddCoreAITool<ListMembershipsTool>(ListMembershipsTool.TheName)
            .WithTitle(S["List Memberships"])
            .WithDescription(S["Lists memberships with optional member search, status and paging: member, product, roles, start and end dates, orders. Use it to see who is a member or to find a membership's id."])
            .WithCategory(S["Memberships"])
            .Selectable();

        services.AddCoreAITool<EndMembershipTool>(EndMembershipTool.TheName)
            .WithTitle(S["End Membership"])
            .WithDescription(S["Ends an active membership now and removes the member from the roles it added. Refunds nothing and cannot be undone from the admin; the member must buy again."])
            .WithCategory(S["Memberships"])
            .AsDestructive()
            .Selectable();
    }
}
