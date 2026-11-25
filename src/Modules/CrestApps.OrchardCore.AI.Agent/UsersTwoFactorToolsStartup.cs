using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that resets the second sign-in factors of a user (two-factor authentication and the authenticator
/// app key), when the Two-Factor Authentication feature of Users is on.
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
        // A new authenticator key cannot be undone, and both factors weaken the sign-in of another person.
        services.AddCoreAITool<ResetUserSignInFactorsTool>(ResetUserSignInFactorsTool.TheName)
            .WithTitle(S["Reset User Sign-In Factors"])
            .WithDescription(S["Resets the second sign-in factors of a user who lost them: switches off two-factor authentication, and can also forget their authenticator app key so a lost device's codes stop working. The user's other sessions are signed out. It refuses for the calling user's own account and never returns a key or a recovery code. Passkeys cannot be removed here: the Orchard Core version this site runs has no passkeys."])
            .WithCategory(S["Users Management"])
            .AsSecurity()
            .Selectable();
    }
}
