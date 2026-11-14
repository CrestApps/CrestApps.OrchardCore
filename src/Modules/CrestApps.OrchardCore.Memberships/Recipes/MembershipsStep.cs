using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Memberships.Recipes;

/// <summary>
/// Adds or updates memberships from a <c>Memberships</c> recipe step. The member is named by <c>UserName</c>, which must
/// exist on the site; a membership is matched by its <c>ItemId</c>, then by its member, its product and when it starts,
/// so a second run changes it instead of adding another. An active membership grants its roles through the role grants,
/// as a bought one does.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "Memberships", "Memberships": [ { "ItemId": "...", "UserName": "ana", "ContentItemId": "...", "Title": "Gold", "RoleNames": [ "Gold member" ], "StartsUtc": "2026-01-01T00:00:00Z", "Status": "Active" } ] }
/// </code>
/// </remarks>
internal sealed class MembershipsStep : CatalogStoreRecipeStep<Membership>
{
    public const string EntriesName = "Memberships";

    /// <summary>
    /// The members of a membership that belong to the site that stores it: the member's user id, and the roles the
    /// membership itself put the member in.
    /// </summary>
    public static readonly string[] SiteOwnedMembers =
    [
        nameof(Membership.UserId),
        nameof(Membership.AddedRoleNames),
    ];

    private readonly IMembershipStore _store;
    private readonly IUserService _userService;
    private readonly IRoleGrantService _roleGrantService;
    private readonly MembershipService _membershipService;

    internal readonly IStringLocalizer S;

    public MembershipsStep(
        IMembershipStore store,
        IUserService userService,
        IRoleGrantService roleGrantService,
        MembershipService membershipService,
        IStringLocalizer<MembershipsStep> stringLocalizer)
        : base(MembershipsConstants.Recipes.Memberships, EntriesName, store)
    {
        _store = store;
        _userService = userService;
        _roleGrantService = roleGrantService;
        _membershipService = membershipService;
        S = stringLocalizer;
    }

    protected override bool CanImport(JsonObject token, RecipeExecutionContext context)
    {
        // The member is found by name on this site; the roles a membership adds are recorded by the role grants here.
        foreach (var member in SiteOwnedMembers)
        {
            token.Remove(member);
        }

        return true;
    }

    protected override async ValueTask<CatalogRecipeLookup<Membership>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var userName = token[nameof(Membership.UserName)]?.GetValue<string>();
        var contentItemId = token[nameof(Membership.ContentItemId)]?.GetValue<string>();
        var startsUtc = default(DateTime);
        var hasStart = token[nameof(Membership.StartsUtc)] is JsonValue value && value.TryGetValue(out startsUtc);

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(contentItemId) || !hasStart || await _userService.GetUserAsync(userName.Trim()) is not User user)
        {
            return CatalogRecipeLookup<Membership>.NotFound;
        }

        var membership = (await _store.ListByUserAsync(user.UserId))
            .FirstOrDefault(entry => entry.ContentItemId == contentItemId && entry.StartsUtc == startsUtc.ToUniversalTime());

        return membership is null ? CatalogRecipeLookup<Membership>.NotFound : CatalogRecipeLookup<Membership>.Found(membership);
    }

    protected override void Preparing(Membership entry, JsonObject token)
    {
        entry.UserName = entry.UserName?.Trim();
        entry.RoleNames ??= [];
        entry.AddedRoleNames ??= [];
        entry.OrderIds ??= [];
    }

    /// <inheritdoc/>
    /// <remarks>The member is looked up by name here, and their id on this site is kept on the membership.</remarks>
    protected override async ValueTask<ValidationResultDetails> ValidateAsync(Membership entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.UserName) || await _userService.GetUserAsync(entry.UserName) is not User user)
        {
            result.Fail(new ValidationResult(S["There's no user named {0}.", entry.UserName]));

            return result;
        }

        entry.UserId = user.UserId;
        entry.UserName = user.UserName;

        if (string.IsNullOrEmpty(entry.ContentItemId))
        {
            result.Fail(new ValidationResult(S["The membership of {0} needs the product it comes from.", entry.UserName]));
        }

        if (entry.EndsUtc.HasValue && entry.EndsUtc <= entry.StartsUtc)
        {
            result.Fail(new ValidationResult(S["The membership of {0} must end after it starts.", entry.UserName]));
        }

        return result;
    }

    protected override async ValueTask CreateAsync(Membership entry)
    {
        await base.CreateAsync(entry);
        await GrantRolesAsync(entry);
    }

    protected override async ValueTask CommitUpdateAsync(Membership entry, JsonObject token)
    {
        await base.CommitUpdateAsync(entry, token);
        await GrantRolesAsync(entry);
    }

    private async Task GrantRolesAsync(Membership membership)
    {
        if (membership.Status != MembershipStatus.Active)
        {
            return;
        }

        // A recipe grants what a purchase may: never an excluded role, and only the store's grantable roles when it names
        // any. An import can't make its member an administrator.
        foreach (var role in await _membershipService.GetGrantableRolesAsync(membership.RoleNames))
        {
            await _roleGrantService.GrantAsync(membership.UserId, role, MembershipRoleGrantSource.SourceName, membership.ItemId);
        }
    }
}
