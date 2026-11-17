using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that sets the values of a custom user settings type on a user, when the Custom User Settings
/// feature of Users is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.UsersCustomUserSettings)]
public sealed class UsersCustomUserSettingsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersCustomUserSettingsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UsersCustomUserSettingsToolsStartup(IStringLocalizer<UsersCustomUserSettingsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // The tool prepares and checks the settings the way createOrUpdateContentItem does a content item. The Custom
        // User Settings feature can be on without the Contents feature, so the helpers are registered here too.
        services.TryAddScoped<IContentItemPayloadAssistanceService, DefaultContentItemPayloadAssistanceService>();
        services.TryAddScoped<ContentItemPreparationService>();

        services.AddCoreAITool<SaveUserCustomSettingsTool>(SaveUserCustomSettingsTool.TheName)
            .WithTitle(S["Save User Custom Settings"])
            .WithDescription(S["Sets the values of one custom user settings type for a user, given the settings type name and the parts and fields to change. Read the current values with getUser and the type's shape with the content type tools first."])
            .WithCategory(S["Users Management"])
            .Selectable();
    }
}
