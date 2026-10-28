using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Reports.Designer.Recipes;

/// <summary>
/// Imports designed reports and views from the <c>ReportDesigns</c> recipe step. Items are matched by
/// <c>ItemId</c> and replaced, or created with that identifier. An item's owner is matched by the
/// <c>OwnerUserName</c> the export wrote, so a report keeps running on a site where its owner has another user id; an
/// item that names no owner the site knows, and has no owner id, is owned by the person who runs the recipe.
/// Share links are never imported. Report groups are imported first, from <c>Groups</c>, matched by <c>ItemId</c> the
/// same way. A report names its group by <c>GroupId</c>, or by <c>Group</c>, a path of names such as
/// <c>Sales / Regional</c> whose missing groups are created; a <c>Category</c> written before report groups names a
/// top-level group the same way.
/// </summary>
public sealed class ReportDesignsRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The name of the recipe step.
    /// </summary>
    public const string Name = "ReportDesigns";

    private readonly ICatalog<ReportDesign> _designs;
    private readonly ICatalog<ReportView> _views;
    private readonly ReportGroupService _groupService;
    private readonly ReportViewSnapshotStore _snapshots;
    private readonly UserManager<IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
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
    /// <param name="httpContextAccessor">The accessor of the request that runs the recipe, whose user owns the items
    /// that name no owner.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignsRecipeStep(
        ICatalog<ReportDesign> designs,
        ICatalog<ReportView> views,
        ReportGroupService groupService,
        ReportViewSnapshotStore snapshots,
        UserManager<IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<ReportDesignsRecipeStep> stringLocalizer)
        : base(Name)
    {
        _designs = designs;
        _views = views;
        _groupService = groupService;
        _snapshots = snapshots;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        await ImportGroupsAsync(context);

        foreach (var node in context.Step["Views"]?.AsArray() ?? [])
        {
            var view = Read<ReportView>(node, context);

            if (view is null)
            {
                continue;
            }

            view.Query = ReportDesignNormalizer.Normalize(view.Query);
            view.RefreshIntervalMinutes = ReportViewRefreshIntervals.Normalize(view.RefreshIntervalMinutes);
            view.OwnerId = await ResolveOwnerAsync(node, view.OwnerId);

            var existing = await _views.FindByIdAsync(view.ItemId);
            var previousQuery = existing is null ? null : ReportDesignService.SerializeQuery(existing.Query);

            if (!await SaveAsync(_views, view, view.DisplayText, context))
            {
                continue;
            }

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
            design.OwnerId = await ResolveOwnerAsync(node, design.OwnerId);
            design.GroupId = await ResolveGroupAsync(node, design);
            await SaveAsync(_designs, design, design.DisplayText, context);
        }
    }

    private async Task ImportGroupsAsync(RecipeExecutionContext context)
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
            return;
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

    private async Task<string> ResolveOwnerAsync(JsonNode node, string ownerId)
    {
        var userName = node?["OwnerUserName"]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(userName))
        {
            var user = await _userManager.FindByNameAsync(userName);

            if (user is not null)
            {
                return await _userManager.GetUserIdAsync(user);
            }
        }

        if (!string.IsNullOrEmpty(ownerId))
        {
            return ownerId;
        }

        // A report a recipe ships for any site, such as a module's ready-made reports, names no owner: the person who
        // runs the recipe owns it, so it runs with their access. A recipe run without a signed-in person, such as at
        // setup, leaves it without an owner.
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated == true &&
            await _userManager.GetUserAsync(principal) is { } importer)
        {
            return await _userManager.GetUserIdAsync(importer);
        }

        return ownerId;
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
}
