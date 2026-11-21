using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Calendars;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Displays an API key in the admin list and edits it.
/// </summary>
public sealed class AIApiKeyDisplayDriver : DisplayDriver<AIApiKey>
{
    // Orchard Core's permission to manage roles; holding it means the editor may give any role.
    private static readonly Permission _manageRoles = new("ManageRoles");

    private readonly IRoleService _roleService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIProfileManager _profileManager;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIApiKeyDisplayDriver"/> class.
    /// </summary>
    /// <param name="roleService">Lists the roles a server key may act with.</param>
    /// <param name="profileManager">Lists the profiles a browser key may chat with.</param>
    /// <param name="localClock">Shows and reads the expiry in the site's time zone.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIApiKeyDisplayDriver(
        IRoleService roleService,
        IAIProfileManager profileManager,
        ILocalClock localClock,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<AIApiKeyDisplayDriver> stringLocalizer)
    {
        _roleService = roleService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _profileManager = profileManager;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(AIApiKey key, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("AIApiKey_Fields_SummaryAdmin", key)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIApiKey_Description_SummaryAdmin", key)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("AIApiKey_DefaultTags_SummaryAdmin", key)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Access:5"),
            View("AIApiKey_Buttons_SummaryAdmin", key).Location("Actions:5"),
            View("AIApiKey_ActionsMenu_SummaryAdmin", key).Location("ActionsMenu:10"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIApiKey key, BuildEditorContext context)
    {
        return Initialize<AIApiKeyViewModel>("AIApiKeyFields_Edit", async model =>
        {
            model.Name = key.Name;
            model.Kind = key.Kind;
            model.IsNew = context.IsNew;
            model.Scopes = key.Scopes?.ToArray() ?? [];
            model.Roles = key.Roles?.ToArray() ?? [];
            model.AllowedOrigins = string.Join(Environment.NewLine, key.AllowedOrigins ?? []);
            model.AllowedProfileIds = key.AllowedProfileIds?.ToArray() ?? [];
            model.Expires = key.ExpiresUtc is { } expiresUtc
                ? (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc))).DateTime
                : null;
            model.Enabled = key.Enabled;
            model.PublicKey = key.PublicKey;
            model.AvailableRoles = (await _roleService.GetRoleNamesAsync())
                .Where(role => !string.Equals(role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(role => new SelectListItem(role, role, model.Roles.Contains(role, StringComparer.OrdinalIgnoreCase)))
                .ToList();
            model.AvailableProfiles = (await _profileManager.GetAllAsync())
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId, model.AllowedProfileIds.Contains(profile.ItemId)))
                .ToList();
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIApiKey key, UpdateEditorContext context)
    {
        var model = new AIApiKeyViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The name is required."]);
        }

        // The kind is chosen once: a server key's secret cannot become public, nor the other way round.
        if (context.IsNew)
        {
            key.Kind = model.Kind;
        }

        var wasEnabled = key.Enabled;
        var previousExpiresUtc = key.ExpiresUtc;

        key.Name = model.Name?.Trim();
        key.Enabled = model.Enabled;
        key.ExpiresUtc = model.Expires is { } expires ? await _localClock.ToUtcLenientAsync(expires) : null;

        // A new key, a key turned back on, or a key given a later expiry (or none) reaches again everything it is set up
        // for, so the editor must be able to grant all of it, not only what they add.
        var reopens = context.IsNew ||
            (key.Enabled && !wasEnabled) ||
            (previousExpiresUtc is { } previous && (key.ExpiresUtc is null || key.ExpiresUtc > previous));

        if (key.Kind == AIApiKeyKind.Browser)
        {
            var origins = (model.AllowedOrigins ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(origin => origin.TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (origins.Count == 0 || origins.Any(origin => !IsOrigin(origin)))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AllowedOrigins), S["Enter each web site as its origin, such as https://www.example.com."]);
            }

            var profiles = (model.AllowedProfileIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();

            if (profiles.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AllowedProfileIds), S["Choose the profiles the key may chat with."]);
            }

            // A browser key lets anyone on its web sites chat with its profiles, so its editor must be able to chat with
            // each profile they add. Profiles the key already had stay, unless the edit opens the key to more: then
            // every profile is checked.
            var widens = reopens || origins.Except(key.AllowedOrigins ?? [], StringComparer.OrdinalIgnoreCase).Any();
            var refusedProfiles = await GetRefusedProfilesAsync(widens ? profiles : profiles.Except(key.AllowedProfileIds ?? [], StringComparer.Ordinal));

            if (refusedProfiles.Count > 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AllowedProfileIds), S["You can't let a key chat with the profile(s) {0}: you may only choose profiles you may chat with.", string.Join(", ", refusedProfiles)]);
            }

            key.AllowedOrigins = origins;
            key.AllowedProfileIds = profiles;
            key.Scopes = [AIApiKeys.Scopes.Chat];
            key.Roles = [];
        }
        else
        {
            var scopes = (model.Scopes ?? []).Where(AIApiKeys.Scopes.All.Contains).Distinct(StringComparer.Ordinal).ToList();

            if (scopes.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Scopes), S["Choose at least one area the key may call."]);
            }

            var roles = (model.Roles ?? []).Where(role => !string.IsNullOrEmpty(role)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (roles.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Roles), S["Choose the roles whose permissions the key acts with."]);
            }

            // A key acts with its roles' permissions, so giving it a role is granting that role: an editor may give only
            // roles they hold, unless they may manage roles. Roles the key already had stay, unless the edit widens the
            // key (more areas, turned back on, a later expiry): its roles then reach more, so every role is checked.
            var widens = reopens || scopes.Except(key.Scopes ?? [], StringComparer.Ordinal).Any();
            var user = _httpContextAccessor.HttpContext?.User;
            var refused = AIApiKeyRoleRules.GetRefusedRoles(
                roles,
                widens ? [] : key.Roles,
                await _roleService.GetRoleNamesAsync(),
                role => user?.IsInRole(role) == true,
                user is not null && await _authorizationService.AuthorizeAsync(user, _manageRoles));

            if (refused.Count > 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Roles), widens && !context.IsNew
                    ? S["You can't widen a key that acts with the role(s) {0}: adding areas, turning it back on or extending its expiry needs roles you hold.", string.Join(", ", refused)]
                    : S["You can't give a key the role(s) {0}: you may only give roles you hold.", string.Join(", ", refused)]);
            }

            key.Scopes = scopes;
            key.Roles = roles;
            key.AllowedOrigins = [];
            key.AllowedProfileIds = [];
        }

        return Edit(key, context);
    }

    // The profiles the editor may not let a browser key chat with: those they may not chat with themselves, by the same
    // check the completion endpoint makes, and those that do not exist.
    private async Task<List<string>> GetRefusedProfilesAsync(IEnumerable<string> profileIds)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var refused = new List<string>();

        foreach (var profileId in profileIds)
        {
            var profile = await _profileManager.FindByIdAsync(profileId);

            if (profile is null || user is null || !await _authorizationService.AuthorizeAsync(user, AIPermissions.QueryAnyAIProfile, profile))
            {
                refused.Add(profile?.DisplayText ?? profile?.Name ?? profileId);
            }
        }

        return refused;
    }

    private static bool IsOrigin(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
            uri.AbsolutePath == "/" &&
            string.IsNullOrEmpty(uri.Query);
}
