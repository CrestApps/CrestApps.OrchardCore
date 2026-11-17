using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that switches off the two-factor authentication of a user, when the Two-Factor Authentication
/// feature of Users is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Users2FA)]
public sealed class UsersTwoFactorToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersTwoFactorToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UsersTwoFactorToolsStartup(IStringLocalizer<UsersTwoFactorToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<DisableUserTwoFactorTool>(DisableUserTwoFactorTool.TheName)
            .WithTitle(S["Disable User Two-Factor Authentication"])
            .WithDescription(S["Switches off two-factor authentication for a user who lost their second factor, so they can sign in with their password and set it up again. It refuses for the calling user's own account."])
            .WithCategory(S["Users Management"])
            .RequireApproval()
            .Selectable();
    }
}
