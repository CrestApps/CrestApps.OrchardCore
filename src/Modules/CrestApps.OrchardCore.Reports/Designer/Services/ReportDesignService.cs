using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Stores designed reports and views. Saving checks the design against the live data sources with the designer's
/// access and reports any problem, but still keeps an unfinished design so work is not lost; such a design shows its
/// problems when it runs. Only saving rules that protect data block a save.
/// </summary>
public sealed class ReportDesignService
{
    private readonly ICatalog<ReportDesign> _designs;
    private readonly ICatalog<ReportView> _views;
    private readonly ReportShareLinkService _shareLinks;
    private readonly ReportViewSnapshotStore _snapshots;
    private readonly ReportQueryPlanner _planner;
    private readonly ReportDesignDocumentBuilder _documentBuilder;
    private readonly IClock _clock;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignService"/> class.
    /// </summary>
    /// <param name="designs">The report catalog.</param>
    /// <param name="views">The view catalog.</param>
    /// <param name="shareLinks">The share link service.</param>
    /// <param name="snapshots">The store of scheduled views' results.</param>
    /// <param name="planner">The query planner.</param>
    /// <param name="documentBuilder">The document builder used to check visuals.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignService(
        ICatalog<ReportDesign> designs,
        ICatalog<ReportView> views,
        ReportShareLinkService shareLinks,
        ReportViewSnapshotStore snapshots,
        ReportQueryPlanner planner,
        ReportDesignDocumentBuilder documentBuilder,
        IClock clock,
        IStringLocalizer<ReportDesignService> stringLocalizer)
    {
        _designs = designs;
        _views = views;
        _shareLinks = shareLinks;
        _snapshots = snapshots;
        _planner = planner;
        _documentBuilder = documentBuilder;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists every designed report, ordered by title.
    /// </summary>
    /// <returns>The reports.</returns>
    public async Task<IReadOnlyList<ReportDesign>> GetAllAsync()
    {
        return (await _designs.GetAllAsync())
            .OrderBy(design => design.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Finds a designed report.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <returns>The report, or <see langword="null"/>.</returns>
    public async Task<ReportDesign> FindAsync(string id)
    {
        return string.IsNullOrEmpty(id) ? null : await _designs.FindByIdAsync(id);
    }

    /// <summary>
    /// Lists every view, ordered by name.
    /// </summary>
    /// <returns>The views.</returns>
    public async Task<IReadOnlyList<ReportView>> GetAllViewsAsync()
    {
        return (await _views.GetAllAsync())
            .OrderBy(view => view.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Finds a view.
    /// </summary>
    /// <param name="id">The view identifier.</param>
    /// <returns>The view, or <see langword="null"/>.</returns>
    public async Task<ReportView> FindViewAsync(string id)
    {
        return string.IsNullOrEmpty(id) ? null : await _views.FindByIdAsync(id);
    }

    /// <summary>
    /// Saves a report, creating it when <paramref name="existing"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="incoming">The report received from the designer.</param>
    /// <param name="existing">The stored report being changed, or <see langword="null"/> for a new one.</param>
    /// <param name="user">The person saving.</param>
    /// <param name="canSharePublicly">Whether the person may share reports publicly.</param>
    /// <returns>The outcome.</returns>
    public async Task<ReportSaveResult> SaveAsync(ReportDesign incoming, ReportDesign existing, ClaimsPrincipal user, bool canSharePublicly)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(user);

        var result = new ReportSaveResult();
        // A report that existed only as an unpublished draft keeps the identifier it was drafted with.
        var design = existing ?? new ReportDesign
        {
            ItemId = incoming.ItemId,
            OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier),
            Author = user.Identity?.Name,
            CreatedUtc = _clock.UtcNow,
        };

        design.DisplayText = ReportDesignNormalizer.Truncate(incoming.DisplayText);
        design.Description = incoming.Description?.Trim();
        design.Category = ReportDesignNormalizer.Truncate(incoming.Category);
        design.Query = ReportDesignNormalizer.Normalize(incoming.Query);
        design.Visuals = ReportDesignNormalizer.Normalize(incoming.Visuals);
        design.ShowInAdminMenu = incoming.ShowInAdminMenu;
        design.AllowExport = incoming.AllowExport;
        design.SharedUserNames = Distinct(incoming.SharedUserNames);
        design.SharedRoles = Distinct(incoming.SharedRoles);

        if (string.IsNullOrEmpty(design.DisplayText))
        {
            result.Errors.Add(S["Enter a title for the report."]);
        }

        var wasPublic = existing?.SharedRoles?.Contains(OrchardCoreConstants.Roles.Anonymous, StringComparer.OrdinalIgnoreCase) == true;

        if (!canSharePublicly && !wasPublic && design.SharedRoles.Contains(OrchardCoreConstants.Roles.Anonymous, StringComparer.OrdinalIgnoreCase))
        {
            result.Errors.Add(S["You are not allowed to share reports with anonymous visitors."]);
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        await CheckAsync(design.Query, design.Visuals, user, result);

        design.ModifiedUtc = existing is null ? null : _clock.UtcNow;

        if (existing is null)
        {
            await _designs.CreateAsync(design);
        }
        else
        {
            await _designs.UpdateAsync(design);
        }

        result.Id = design.ItemId;
        result.Design = design;
        result.Saved = true;

        return result;
    }

    /// <summary>
    /// Saves a view, creating it when <paramref name="existing"/> is <see langword="null"/>. The stored result of a
    /// scheduled view is deleted when its query changes or it becomes live, so reports never read rows the view no
    /// longer returns.
    /// </summary>
    /// <param name="incoming">The view received from the designer.</param>
    /// <param name="existing">The stored view being changed, or <see langword="null"/> for a new one.</param>
    /// <param name="user">The person saving.</param>
    /// <returns>The outcome.</returns>
    public async Task<ReportSaveResult> SaveViewAsync(ReportView incoming, ReportView existing, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(user);

        var result = new ReportSaveResult();
        var previousQuery = existing is null ? null : SerializeQuery(existing.Query);
        var view = existing ?? new ReportView
        {
            OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier),
            Author = user.Identity?.Name,
            CreatedUtc = _clock.UtcNow,
        };

        view.DisplayText = ReportDesignNormalizer.Truncate(incoming.DisplayText);
        view.Description = incoming.Description?.Trim();
        view.Query = ReportDesignNormalizer.Normalize(incoming.Query);
        view.RefreshIntervalMinutes = ReportViewRefreshIntervals.Normalize(incoming.RefreshIntervalMinutes);

        if (string.IsNullOrEmpty(view.DisplayText))
        {
            result.Errors.Add(S["Enter a name for the view."]);

            return result;
        }

        if (existing is not null && view.Query.DataSets.Any(dataSet =>
            dataSet.Source == ReportsConstants.ViewsDataSource && dataSet.DataSet == existing.ItemId))
        {
            result.Errors.Add(S["A view cannot read itself."]);

            return result;
        }

        view.ModifiedUtc = existing is null ? null : _clock.UtcNow;

        if (existing is null)
        {
            await _views.CreateAsync(view);
        }
        else
        {
            await _views.UpdateAsync(view);

            if (view.RefreshIntervalMinutes == ReportViewRefreshIntervals.Live || HasQueryChanged(previousQuery, view.Query))
            {
                await _snapshots.DeleteAsync(view.ItemId);
            }
        }

        await CheckAsync(view.Query, null, user, result);

        result.Id = view.ItemId;
        result.Saved = true;

        return result;
    }

    /// <summary>
    /// Deletes a report and its share links.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteAsync(ReportDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);

        await _shareLinks.DeleteAllAsync(design.ItemId);
        await _designs.DeleteAsync(design);
    }

    /// <summary>
    /// Deletes a view and its stored result.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteViewAsync(ReportView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        await _snapshots.DeleteAsync(view.ItemId);
        await _views.DeleteAsync(view);
    }

    /// <summary>
    /// Finds the reports and views that read a view.
    /// </summary>
    /// <param name="viewId">The view identifier.</param>
    /// <returns>The titles of the reports and views that use it.</returns>
    public async Task<IReadOnlyList<string>> FindViewUsagesAsync(string viewId)
    {
        bool Uses(ReportQueryDefinition query)
        {
            return query?.DataSets?.Any(dataSet => dataSet.Source == ReportsConstants.ViewsDataSource && dataSet.DataSet == viewId) == true;
        }

        var designs = (await _designs.GetAllAsync()).Where(design => Uses(design.Query)).Select(design => design.DisplayText);
        var views = (await _views.GetAllAsync()).Where(view => view.ItemId != viewId && Uses(view.Query)).Select(view => view.DisplayText);

        return designs.Concat(views).ToArray();
    }

    private async Task CheckAsync(ReportQueryDefinition query, IList<ReportVisualDefinition> visuals, ClaimsPrincipal user, ReportSaveResult result)
    {
        try
        {
            var plan = await _planner.PlanAsync(query, new ReportDataSourceContext { User = user });

            foreach (var error in plan.Errors)
            {
                result.Warnings.Add(error);
            }

            if (plan.IsValid && visuals is not null)
            {
                var columns = plan.Columns
                    .Select(column => new ReportResultColumn
                    {
                        Id = column.Definition.Id,
                        Label = column.Label,
                        DataType = column.DataType,
                        IsMeasure = column.IsMeasure,
                    })
                    .ToArray();

                foreach (var error in _documentBuilder.Validate(visuals, columns))
                {
                    result.Warnings.Add(error);
                }
            }
        }
        catch (ReportQueryException exception)
        {
            foreach (var error in exception.Errors)
            {
                result.Warnings.Add(error);
            }
        }
    }

    /// <summary>
    /// Serializes a view query so it can be compared with another one.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The JSON of the query, or <see langword="null"/>.</returns>
    internal static string SerializeQuery(ReportQueryDefinition query)
    {
        return query is null ? null : JsonSerializer.Serialize(query, ReportDesignerJson.Options);
    }

    /// <summary>
    /// Determines whether a view query differs from the one it had before.
    /// </summary>
    /// <param name="previousQuery">The JSON of the earlier query, from <see cref="SerializeQuery"/>.</param>
    /// <param name="query">The query now.</param>
    /// <returns><see langword="true"/> when the query changed.</returns>
    internal static bool HasQueryChanged(string previousQuery, ReportQueryDefinition query)
    {
        return !string.Equals(previousQuery, SerializeQuery(query), StringComparison.Ordinal);
    }

    private static List<string> Distinct(IEnumerable<string> values)
    {
        return (values ?? [])
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(500)
            .ToList();
    }
}

/// <summary>
/// The outcome of saving a report or view.
/// </summary>
public sealed class ReportSaveResult
{
    /// <summary>
    /// Gets or sets the identifier of the saved item.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the saved report, when a report was saved.
    /// </summary>
    public ReportDesign Design { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item was saved.
    /// </summary>
    public bool Saved { get; set; }

    /// <summary>
    /// Gets the problems that prevented saving.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets the problems found in a saved item, which it shows when it runs until they are fixed.
    /// </summary>
    public IList<string> Warnings { get; } = [];
}
