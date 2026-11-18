using CrestApps.Core.AI;
using CrestApps.OrchardCore.Users.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Registers the user name AI tool whenever the User Display Name and AI features are on. It needs <c>EditUsers</c> on the
/// user, as the user editor's name card does.
/// </summary>
[Feature(UsersConstants.Feature.DisplayName)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class DisplayNameAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayNameAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisplayNameAIToolsStartup(IStringLocalizer<DisplayNameAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SetUserDisplayNameTool>(SetUserDisplayNameTool.TheName)
            .WithTitle(S["Set User Display Name"])
            .WithDescription(S["Sets a user's first, middle, last and display name. Use it after searchForUsers; it applies the required-field rules from the User Display Name settings and fails when a required part is missing."])
            .WithCategory(S["Users"])
            .Selectable();
    }
}

/// <summary>
/// Registers the user avatar AI tool whenever the User Avatar and AI features are on. It needs <c>ManageMedia</c> and
/// <c>EditUsers</c> on the user, as the user editor's avatar card does.
/// </summary>
[Feature(UsersConstants.Feature.Avatars)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AvatarAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvatarAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AvatarAIToolsStartup(IStringLocalizer<AvatarAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SetUserAvatarTool>(SetUserAvatarTool.TheName)
            .WithTitle(S["Set User Avatar"])
            .WithDescription(S["Sets or clears a user's avatar from a media library path. The file must already be in the media library and have an allowed image extension; clearing fails when avatars are required."])
            .WithCategory(S["Users"])
            .Selectable();
    }
}
