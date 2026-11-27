using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core.Handlers;

/// <summary>
/// Guards a content part that names the roles a purchase grants, such as a membership product or a subscription plan,
/// on every save, not only in its editor: the content API, a content import and a recipe never run display drivers. A
/// signed-in user who may not assign roles to users can't add a role the stored item doesn't already grant; the save
/// fails validation and nothing is stored. Saves with no signed-in user (setup, background work, a remote deployment)
/// are trusted, as their content comes from whoever configured them.
/// </summary>
/// <typeparam name="TPart">The part that names the roles.</typeparam>
public abstract class GrantedRolesPartHandler<TPart> : ContentPartHandler<TPart>
    where TPart : ContentPart, new()
{
    // The roles every version of each item this request loaded granted, before anything changed them: a role counts as
    // already granted only when each loaded version grants it. An audit trail Restore loads the old version and the
    // current one, so a role removed since counts as added and needs the right to assign roles again.
    private readonly Dictionary<string, HashSet<string>> _storedRoles = new(StringComparer.Ordinal);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    protected readonly IStringLocalizer S;

    protected GrantedRolesPartHandler(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the roles the part grants.
    /// </summary>
    /// <param name="part">The part.</param>
    protected abstract IEnumerable<string> GetRoleNames(TPart part);

    /// <summary>
    /// Returns whether <paramref name="user"/> may choose the roles the part grants: by default, whether they may assign
    /// roles to users.
    /// </summary>
    /// <param name="user">The signed-in user saving the item.</param>
    protected virtual async Task<bool> CanChooseRolesAsync(ClaimsPrincipal user)
        => await _authorizationService.AuthorizeAsync(user, UsersPermissions.AssignRoleToUsers);

    // Orchard Core's content manager loads every stored version it hands out (the content API, an import and the editor
    // all get the item from it before changing it), so what they held is known before the change.
    public override Task LoadedAsync(LoadContentContext context, TPart part)
    {
        if (!string.IsNullOrEmpty(context.ContentItem?.ContentItemId))
        {
            var roles = Normalize(GetRoleNames(part));

            if (!_storedRoles.TryGetValue(context.ContentItem.ContentItemId, out var stored))
            {
                _storedRoles[context.ContentItem.ContentItemId] = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                stored.IntersectWith(roles);
            }
        }

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidateContentContext context, TPart part)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        _storedRoles.TryGetValue(context.ContentItem.ContentItemId ?? string.Empty, out var stored);

        var added = Normalize(GetRoleNames(part)).Where(role => stored?.Contains(role) != true).ToArray();

        if (added.Length == 0 || await CanChooseRolesAsync(user))
        {
            return;
        }

        context.Fail(S["Only someone who may assign roles to users can choose the roles this item grants: {0}.", string.Join(", ", added)], typeof(TPart).Name);
    }

    private static IEnumerable<string> Normalize(IEnumerable<string> roleNames)
        => (roleNames ?? []).Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim());
}
