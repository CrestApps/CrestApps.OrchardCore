using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
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
/// <c>OwnerUserName</c> the export wrote, so a report keeps running on a site where its owner has another user id.
/// Share links are never imported.
/// </summary>
public sealed class ReportDesignsRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The name of the recipe step.
    /// </summary>
    public const string Name = "ReportDesigns";

    private readonly ICatalog<ReportDesign> _designs;
    private readonly ICatalog<ReportView> _views;
    private readonly ReportViewSnapshotStore _snapshots;
    private readonly UserManager<IUser> _userManager;
    private readonly IClock _clock;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignsRecipeStep"/> class.
    /// </summary>
    /// <param name="designs">The report catalog.</param>
    /// <param name="views">The view catalog.</param>
    /// <param name="snapshots">The store of scheduled views' results.</param>
    /// <param name="userManager">The user manager used to match owners by user name.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignsRecipeStep(
        ICatalog<ReportDesign> designs,
        ICatalog<ReportView> views,
        ReportViewSnapshotStore snapshots,
        UserManager<IUser> userManager,
        IClock clock,
        IStringLocalizer<ReportDesignsRecipeStep> stringLocalizer)
        : base(Name)
    {
        _designs = designs;
        _views = views;
        _snapshots = snapshots;
        _userManager = userManager;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
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
            design.SharedUserNames ??= [];
            design.SharedRoles ??= [];
            design.OwnerId = await ResolveOwnerAsync(node, design.OwnerId);
            await SaveAsync(_designs, design, design.DisplayText, context);
        }
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
