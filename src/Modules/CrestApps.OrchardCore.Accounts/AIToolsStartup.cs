using CrestApps.Core.AI;
using CrestApps.OrchardCore.Accounts.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Accounts;

/// <summary>
/// Registers the account search AI tool whenever the Accounts and AI features are on. It needs <c>ListContent</c>, and
/// searches only the account types the caller may view, as the account picker's search does.
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
        services.AddCoreAITool<ListAccountsTool>(ListAccountsTool.TheName)
            .WithTitle(S["List Accounts"])
            .WithDescription(S["Finds accounts (businesses and households) by name across every account content type and returns id, name, type, status, member count, locations and owner. Use it to get the account id; use getContentItemById for the full record."])
            .WithCategory(S["Accounts"])
            .Selectable();
    }
}

/// <summary>
/// Registers the account members AI tool whenever the Account Members and AI features are on. It needs
/// <c>ManageAccountMembers</c> and <c>EditContent</c> on the account, and asks for approval, because members are who may
/// act and buy for the account.
/// </summary>
[Feature(AccountsConstants.MembersFeature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class MembersAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MembersAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MembersAIToolsStartup(IStringLocalizer<MembersAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SetAccountMembersTool>(SetAccountMembersTool.TheName)
            .WithTitle(S["Set Account Members"])
            .WithDescription(S["Replaces the users who sign in for an account and their role (Member or Admin), by user name or email. Each user must exist and can belong to one account only; the tool fails without changing anything when one does not qualify."])
            .WithCategory(S["Accounts"])
            .RequireApproval()
            .Selectable();
    }
}

/// <summary>
/// Registers the account location AI tools whenever the Account Locations and AI features are on. Both need
/// <c>EditContent</c> on the account; removing a location asks for approval.
/// </summary>
[Feature(AccountsConstants.LocationsFeature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class LocationsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocationsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LocationsAIToolsStartup(IStringLocalizer<LocationsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SaveAccountLocationTool>(SaveAccountLocationTool.TheName)
            .WithTitle(S["Save Account Location"])
            .WithDescription(S["Adds a named location to an account or updates one by its location id, after checking the address against its country's rules."])
            .WithCategory(S["Accounts"])
            .Selectable();

        services.AddCoreAITool<DeleteAccountLocationTool>(DeleteAccountLocationTool.TheName)
            .WithTitle(S["Delete Account Location"])
            .WithDescription(S["Removes a location from an account by its location id. Orders and work orders that already used it keep their copy of the address."])
            .WithCategory(S["Accounts"])
            .RequireApproval()
            .Selectable();
    }
}

/// <summary>
/// Registers the account team AI tool whenever the Account Team and AI features are on. It needs <c>EditContent</c> on
/// the account.
/// </summary>
[Feature(AccountsConstants.TeamFeature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class TeamAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TeamAIToolsStartup(IStringLocalizer<TeamAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SetAccountTeamTool>(SetAccountTeamTool.TheName)
            .WithTitle(S["Set Account Team"])
            .WithDescription(S["Sets the staff member who owns an account and the other staff on its account team, by staff member id. Find the ids with the staff search tool first."])
            .WithCategory(S["Accounts"])
            .Selectable();
    }
}
