using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Recipes;

/// <summary>
/// Imports designed reports and views from the <c>ReportDesigns</c> recipe step. Items are matched by
/// <c>ItemId</c> and replaced, or created with that identifier. An item's owner is matched by the
/// <c>OwnerUserName</c> the export wrote, so a report keeps running on a site where its owner has another user id; an
/// item that names no owner the site knows, and has no owner id, is owned by the step's default owner: the user the step
/// names in <c>DefaultOwnerUserName</c>, else the site's super user when the step sets <c>OwnedBySuperUser</c> (during
/// setup, the super user setup is creating), else the person who runs the recipe. Share links are never imported.
/// Report groups are imported first, from <c>Groups</c>, matched by <c>ItemId</c> the same way. A report names its group by <c>GroupId</c>, or by <c>Group</c>, a path of names such as
/// <c>Sales / Regional</c> whose missing groups are created; a <c>Category</c> written before report groups names a
/// top-level group the same way.
/// </summary>
public sealed class ReportDesignsRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The name of the recipe step.
    /// </summary>
    public const string Name = "ReportDesigns";

    /// <summary>
    /// The step property that names, by user name, the user who owns the items that name no owner.
    /// </summary>
    public const string DefaultOwnerUserNameProperty = "DefaultOwnerUserName";

    /// <summary>
    /// The step property that, when <see langword="true"/>, gives the items that name no owner to the site's super user.
    /// </summary>
    public const string OwnedBySuperUserProperty = "OwnedBySuperUser";

    // The recipe environment values Orchard Core's setup sets before it runs the setup recipe, which it runs before it
    // creates the super user with that id.
    private const string SetupAdminUserId = "AdminUserId";
    private const string SetupAdminUserName = "AdminUsername";

    private readonly ICatalog<ReportDesign> _designs;
    private readonly ICatalog<ReportView> _views;
    private readonly ReportGroupService _groupService;
    private readonly ReportViewSnapshotStore _snapshots;
    private readonly UserManager<IUser> _userManager;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignsRecipeStep"/> class.
    /// </summary>
    /// <param name="designs">The report catalog.</param>
    /// <param name="views">The view catalog.</param>
    /// <param name="groupService">The report group service, which saves the groups and finds or creates the group a
    /// report names.</param>
    /// <param name="snapshots">The store of scheduled views' results.</param>
    /// <param name="userManager">The user manager used to match owners by user name.</param>
    /// <param name="siteService">The site service, which names the site's super user.</param>
    /// <param name="httpContextAccessor">The accessor of the request that runs the recipe, whose user owns the items
    /// that name no owner.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="logger">The logger, or <see langword="null"/> to log nothing.</param>
    public ReportDesignsRecipeStep(
        ICatalog<ReportDesign> designs,
        ICatalog<ReportView> views,
        ReportGroupService groupService,
        ReportViewSnapshotStore snapshots,
        UserManager<IUser> userManager,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<ReportDesignsRecipeStep> stringLocalizer,
        ILogger<ReportDesignsRecipeStep> logger = null)
        : base(Name)
    {
        _designs = designs;
        _views = views;
        _groupService = groupService;
        _snapshots = snapshots;
        _userManager = userManager;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
        _logger = logger ?? NullLogger<ReportDesignsRecipeStep>.Instance;
    }

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var groupCount = await ImportGroupsAsync(context);
        var defaultOwner = new Lazy<Task<ReportOwner>>(() => ResolveDefaultOwnerAsync(context));
        var viewCount = 0;
        var designCount = 0;

        foreach (var node in context.Step["Views"]?.AsArray() ?? [])
        {
            var view = Read<ReportView>(node, context);

            if (view is null)
            {
                continue;
            }

            view.Query = ReportDesignNormalizer.Normalize(view.Query);
            view.RefreshIntervalMinutes = ReportViewRefreshIntervals.Normalize(view.RefreshIntervalMinutes);
            (view.OwnerId, view.Author) = await ResolveOwnerAsync(node, view.OwnerId, view.Author, defaultOwner);

            var existing = await _views.FindByIdAsync(view.ItemId);
            var previousQuery = existing is null ? null : ReportDesignService.SerializeQuery(existing.Query);

            if (!await SaveAsync(_views, view, view.DisplayText, context))
            {
                continue;
            }

            viewCount++;

            // A replaced view's stored result is dropped when it would no longer match, as saving it in the builder does.
            if (existing is not null &&
                (view.RefreshIntervalMinutes == ReportViewRefreshIntervals.Live || ReportDesignService.HasQueryChanged(previousQuery, view.Query)))
            {
                await _snapshots.DeleteAsync(view.ItemId);
            }
        }

        foreach (var node in context.Step["Reports"]?.AsArray() ?? [])
        {
            var design = Read<ReportDesign>(node, context);

            if (design is null)
            {
                continue;
            }

            design.Query = ReportDesignNormalizer.Normalize(design.Query);
            design.Visuals = ReportDesignNormalizer.Normalize(design.Visuals);
            design.Containers = ReportDesignNormalizer.Normalize(design.Containers);
            design.Queries = ReportDesignNormalizer.Normalize(design.Queries);
            design.SharedUserNames ??= [];
            design.SharedRoles ??= [];
            (design.OwnerId, design.Author) = await ResolveOwnerAsync(node, design.OwnerId, design.Author, defaultOwner);
            design.GroupId = await ResolveGroupAsync(node, design);

            if (await SaveAsync(_designs, design, design.DisplayText, context))
            {
                designCount++;
            }
        }

        ReportsModuleLog.RecipeStepImported(_logger, designCount, viewCount, groupCount);
    }

    private async Task<int> ImportGroupsAsync(RecipeExecutionContext context)
    {
        var imported = new List<ReportGroup>();

        foreach (var node in context.Step["Groups"]?.AsArray() ?? [])
        {
            var group = Read<ReportGroup>(node, context);

            if (group is null)
            {
                continue;
            }

            group.DisplayText = ReportDesignNormalizer.Truncate(group.DisplayText);
            group.ParentId = ReportDesignNormalizer.NormalizeId(group.ParentId);

            if (string.IsNullOrEmpty(group.DisplayText))
            {
                context.Errors.Add(S["The report group '{0}' in the recipe has no name.", group.ItemId]);

                continue;
            }

            await _groupService.ImportAsync(group);
            imported.Add(group);
        }

        if (imported.Count == 0)
        {
            return 0;
        }

        // A parent can come later in the step, so parents are checked once every group is in. A group whose parent is
        // missing, or whose parents lead back to itself, is moved to the top level.
        var tree = await _groupService.GetTreeAsync();

        foreach (var group in imported)
        {
            if (group.ParentId is not null && tree.GetParentId(group.ItemId) is null)
            {
                group.ParentId = null;
                await _groupService.ImportAsync(group);
            }
        }

        return imported.Count;
    }

    // The report's group: the one its GroupId names when it exists, otherwise the group its Group path (or the Category
    // of a report written before report groups) leads to, created when missing.
    private async Task<string> ResolveGroupAsync(JsonNode node, ReportDesign design)
    {
        var groupId = ReportDesignNormalizer.NormalizeId(design.GroupId);

        if (groupId is not null && await _groupService.FindAsync(groupId) is not null)
        {
            return groupId;
        }

#pragma warning disable CS0618 // Category is read only to place a report written before report groups.
        var category = design.Category;
        design.Category = null;
#pragma warning restore CS0618

        if (ReadString(node, "group") is { } path)
        {
            return await _groupService.FindOrCreatePathAsync(ReportGroupService.SplitPath(path));
        }

        return string.IsNullOrWhiteSpace(category) ? null : await _groupService.FindOrCreatePathAsync([category]);
    }

    // A string property of a recipe item, whichever case its name is written in.
    private static string ReadString(JsonNode node, string name)
    {
        if (node is not JsonObject item)
        {
            return null;
        }

        foreach (var (key, value) in item)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase) && value is JsonValue text && text.TryGetValue<string>(out var result))
            {
                return string.IsNullOrWhiteSpace(result) ? null : result;
            }
        }

        return null;
    }

    private T Read<T>(JsonNode node, RecipeExecutionContext context)
        where T : CatalogItem
    {
        try
        {
            var item = node?.Deserialize<T>(ReportDesignerJson.Options);

            if (item is null || string.IsNullOrWhiteSpace(item.ItemId) || item.ItemId.Length > 64 || !item.ItemId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            {
                context.Errors.Add(S["A report design in the recipe has no valid ItemId."]);

                return null;
            }

            return item;
        }
        catch (JsonException exception)
        {
            context.Errors.Add(S["A report design in the recipe could not be read: {0}", exception.Message]);

            return null;
        }
    }

    private async Task<(string OwnerId, string Author)> ResolveOwnerAsync(JsonNode node, string ownerId, string author, Lazy<Task<ReportOwner>> defaultOwner)
    {
        var userName = ReadString(node, "OwnerUserName");

        if (userName is not null)
        {
            var user = await _userManager.FindByNameAsync(userName);

            if (user is not null)
            {
                return (await _userManager.GetUserIdAsync(user), author);
            }
        }

        if (!string.IsNullOrEmpty(ownerId))
        {
            return (ownerId, author);
        }

        // A report a recipe ships for any site, such as a module's ready-made reports, names no owner: the step's default
        // owner owns it, so it runs with their access. A recipe run without one, such as at setup when the step neither
        // names a user nor asks for the super user, leaves it without an owner.
        var owner = await defaultOwner.Value;

        if (owner is not null)
        {
            return (owner.UserId, string.IsNullOrWhiteSpace(author) ? owner.UserName : author);
        }

        return (ownerId, author);
    }

    // The owner of the items that name no owner: the user DefaultOwnerUserName names, else the super user when the step
    // sets OwnedBySuperUser, else the signed-in person who runs the recipe; null when there is none.
    private async Task<ReportOwner> ResolveDefaultOwnerAsync(RecipeExecutionContext context)
    {
        if (ReadString(context.Step, DefaultOwnerUserNameProperty) is { } userName)
        {
            if (await _userManager.FindByNameAsync(userName) is { } named)
            {
                return new ReportOwner(await _userManager.GetUserIdAsync(named), named.UserName ?? userName);
            }

            ReportsModuleLog.RecipeStepDefaultOwnerMissing(_logger, userName.SanitizeLogValue());
        }

        if (ReadBoolean(context.Step, OwnedBySuperUserProperty) && await ResolveSuperUserAsync(context) is { } superUser)
        {
            return superUser;
        }

        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated == true &&
            await _userManager.GetUserAsync(principal) is { } importer)
        {
            return new ReportOwner(await _userManager.GetUserIdAsync(importer), importer.UserName);
        }

        return null;
    }

    // The site's super user. Orchard Core's setup runs the setup recipe before it creates the super user, but it puts
    // the user's id and user name in the recipe environment first, so a step that setup runs reads them there.
    // Otherwise the site settings name the super user, who owns the items only while the account exists and is enabled.
    private async Task<ReportOwner> ResolveSuperUserAsync(RecipeExecutionContext context)
    {
        if (context.Environment is IDictionary<string, object> environment &&
            environment.TryGetValue(SetupAdminUserId, out var setupUserId) &&
            Convert.ToString(setupUserId, CultureInfo.InvariantCulture) is { Length: > 0 } adminUserId)
        {
            environment.TryGetValue(SetupAdminUserName, out var setupUserName);

            return new ReportOwner(adminUserId, Convert.ToString(setupUserName, CultureInfo.InvariantCulture));
        }

        var site = await _siteService.GetSiteSettingsAsync();

        if (string.IsNullOrEmpty(site?.SuperUser) ||
            await _userManager.FindByIdAsync(site.SuperUser) is not { } superUser ||
            superUser is User { IsEnabled: false })
        {
            ReportsModuleLog.RecipeStepSuperUserMissing(_logger);

            return null;
        }

        return new ReportOwner(await _userManager.GetUserIdAsync(superUser), superUser.UserName);
    }

    // A boolean property of the step, whichever case its name is written in, given as true or "true".
    private static bool ReadBoolean(JsonNode node, string name)
    {
        if (node is not JsonObject item)
        {
            return false;
        }

        foreach (var (key, value) in item)
        {
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase) || value is not JsonValue flag)
            {
                continue;
            }

            if (flag.TryGetValue<bool>(out var result))
            {
                return result;
            }

            return flag.TryGetValue<string>(out var text) && bool.TryParse(text, out result) && result;
        }

        return false;
    }

    private async Task<bool> SaveAsync<T>(ICatalog<T> catalog, T item, string displayText, RecipeExecutionContext context)
        where T : CatalogItem
    {
        if (string.IsNullOrWhiteSpace(displayText))
        {
            context.Errors.Add(S["The report design '{0}' in the recipe has no title.", item.ItemId]);

            return false;
        }

        if (item is IModifiedUtcAwareModel modified)
        {
            modified.ModifiedUtc = _clock.UtcNow;
        }

        if (await catalog.FindByIdAsync(item.ItemId) is null)
        {
            await catalog.CreateAsync(item);
        }
        else
        {
            await catalog.UpdateAsync(item);
        }

        return true;
    }

    private sealed record ReportOwner(string UserId, string UserName);
}
