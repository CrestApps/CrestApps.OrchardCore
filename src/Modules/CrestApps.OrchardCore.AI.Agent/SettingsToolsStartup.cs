using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, read and change the site settings pages, through the same display drivers the
/// settings pages use. The Settings feature is always on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Settings)]
public sealed class SettingsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SettingsToolsStartup(IStringLocalizer<SettingsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListSiteSettingsGroupsTool>(ListSiteSettingsGroupsTool.TheName)
            .WithTitle(S["List Site Settings Groups"])
            .WithDescription(S["Lists the site settings pages the current user can manage, with group id, title, menu path and link. Call it first to find the group id for getSiteSettings."])
            .WithCategory(S["Site Settings"])
            .Selectable();

        services.AddCoreAITool<GetSiteSettingsTool>(GetSiteSettingsTool.TheName)
            .WithTitle(S["Get Site Settings"])
            .WithDescription(S["Returns the current values of one settings group and a JSON schema of its editable fields. Secrets are never returned, only whether a value is set and the secret name."])
            .WithCategory(S["Site Settings"])
            .Selectable();

        services.AddCoreAITool<UpdateSiteSettingsTool>(UpdateSiteSettingsTool.TheName)
            .WithTitle(S["Update Site Settings"])
            .WithDescription(S["Changes fields of one settings group; send only the fields to change, in the shape returned by getSiteSettings. Returns validation errors, or the saved values and whether the site is reloading."])
            .WithCategory(S["Site Settings"])
            .AsSecurity()
            .Selectable();
    }
}
