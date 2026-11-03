using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Displays an API key in the admin list and edits it.
/// </summary>
public sealed class AIApiKeyDisplayDriver : DisplayDriver<AIApiKey>
{
    private readonly IRoleService _roleService;
    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIApiKeyDisplayDriver"/> class.
    /// </summary>
    /// <param name="roleService">Lists the roles a server key may act with.</param>
    /// <param name="profileManager">Lists the profiles a browser key may chat with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIApiKeyDisplayDriver(IRoleService roleService, IAIProfileManager profileManager, IStringLocalizer<AIApiKeyDisplayDriver> stringLocalizer)
    {
        _roleService = roleService;
        _profileManager = profileManager;
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
            model.ExpiresUtc = key.ExpiresUtc;
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

        key.Name = model.Name?.Trim();
        key.Enabled = model.Enabled;
        key.ExpiresUtc = model.ExpiresUtc is { } expires ? DateTime.SpecifyKind(expires, DateTimeKind.Utc) : null;

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

            key.Scopes = scopes;
            key.Roles = roles;
            key.AllowedOrigins = [];
            key.AllowedProfileIds = [];
        }

        return Edit(key, context);
    }

    private static bool IsOrigin(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
            uri.AbsolutePath == "/" &&
            string.IsNullOrEmpty(uri.Query);
}
