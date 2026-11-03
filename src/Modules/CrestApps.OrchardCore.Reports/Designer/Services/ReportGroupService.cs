using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Stores report groups. A group cannot be nested in itself or in a group nested in it, two groups that share a parent
/// cannot have the same name, and a group that still holds groups or reports cannot be deleted. The groups saved through
/// the service are part of its tree at once, even before the request commits them, so a recipe or an upgrade step that
/// creates several groups sees each one it created.
/// </summary>
public sealed class ReportGroupService
{
    private readonly ICatalog<ReportGroup> _groups;
    private readonly ICatalog<ReportDesign> _designs;
    private readonly IClock _clock;
    private readonly IStringLocalizer S;

    // The groups saved (or, as null, deleted) in this scope, which the catalog's cached copy shows only once committed.
    private readonly Dictionary<string, ReportGroup> _changed = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportGroupService"/> class.
    /// </summary>
    /// <param name="groups">The group catalog.</param>
    /// <param name="designs">The report catalog.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportGroupService(
        ICatalog<ReportGroup> groups,
        ICatalog<ReportDesign> designs,
        IClock clock,
        IStringLocalizer<ReportGroupService> stringLocalizer)
    {
        _groups = groups;
        _designs = designs;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Builds the tree of every group.
    /// </summary>
    /// <returns>The tree.</returns>
    public async Task<ReportGroupTree> GetTreeAsync()
    {
        var groups = (await _groups.GetAllAsync()).ToDictionary(group => group.ItemId, StringComparer.Ordinal);

        foreach (var (id, group) in _changed)
        {
            if (group is null)
            {
                groups.Remove(id);
            }
            else
            {
                groups[id] = group;
            }
        }

        return new ReportGroupTree(groups.Values);
    }

    /// <summary>
    /// Finds a group.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <returns>The group, or <see langword="null"/>.</returns>
    public async Task<ReportGroup> FindAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return _changed.TryGetValue(id, out var changed) ? changed : await _groups.FindByIdAsync(id);
    }

    /// <summary>
    /// Saves a group, creating it when <paramref name="existing"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="incoming">The name, parent, and order entered.</param>
    /// <param name="existing">The stored group being changed, or <see langword="null"/> for a new one.</param>
    /// <returns>The outcome.</returns>
    public async Task<ReportGroupResult> SaveAsync(ReportGroup incoming, ReportGroup existing)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var result = new ReportGroupResult();
        var tree = await GetTreeAsync();
        var group = existing ?? new ReportGroup
        {
            CreatedUtc = _clock.UtcNow,
        };

        group.DisplayText = ReportDesignNormalizer.Truncate(incoming.DisplayText);
        group.ParentId = ReportDesignNormalizer.NormalizeId(incoming.ParentId);
        group.Order = incoming.Order;

        if (string.IsNullOrEmpty(group.DisplayText))
        {
            result.Errors.Add(S["Enter a name for the group."]);
        }

        if (group.ParentId is not null)
        {
            if (tree.Find(group.ParentId) is null)
            {
                result.Errors.Add(S["The parent group no longer exists."]);
            }
            else if (existing is not null && tree.IsSelfOrDescendant(group.ParentId, existing.ItemId))
            {
                result.Errors.Add(S["A group cannot be placed in itself or in a group nested in it."]);
            }
        }

        if (!string.IsNullOrEmpty(group.DisplayText) &&
            tree.GetChildren(group.ParentId).Any(sibling => sibling.ItemId != existing?.ItemId &&
                string.Equals(sibling.DisplayText, group.DisplayText, StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add(S["A group named '{0}' already exists there.", group.DisplayText]);
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        group.ModifiedUtc = existing is null ? null : _clock.UtcNow;

        if (existing is null)
        {
            await _groups.CreateAsync(group);
        }
        else
        {
            await _groups.UpdateAsync(group);
        }

        _changed[group.ItemId] = group;
        result.Group = group;
        result.Succeeded = true;

        return result;
    }

    /// <summary>
    /// Counts what a group holds directly: the groups nested in it and the reports placed in it.
    /// </summary>
    /// <param name="groupId">The group identifier.</param>
    /// <returns>The numbers of child groups and of reports.</returns>
    public async Task<(int Groups, int Reports)> CountContentsAsync(string groupId)
    {
        var tree = await GetTreeAsync();
        var reports = (await _designs.GetAllAsync()).Count(design => string.Equals(design.GroupId, groupId, StringComparison.Ordinal));

        return (tree.GetChildren(groupId).Count, reports);
    }

    /// <summary>
    /// Deletes a group that holds no groups and no reports.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The outcome; it explains why a group that is not empty was kept.</returns>
    public async Task<ReportGroupResult> DeleteAsync(ReportGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var result = new ReportGroupResult { Group = group };
        var (groups, reports) = await CountContentsAsync(group.ItemId);

        if (groups > 0 || reports > 0)
        {
            result.Errors.Add(S["The group '{0}' holds {1} group(s) and {2} report(s). Move them to another group, or out of any group, before deleting it.", group.DisplayText, groups, reports]);

            return result;
        }

        await _groups.DeleteAsync(group);
        _changed[group.ItemId] = null;
        result.Succeeded = true;

        return result;
    }

    /// <summary>
    /// Finds the group a path of names leads to, such as <c>Sales / Regional</c>, creating the groups that do not exist
    /// yet. Names are matched ignoring case.
    /// </summary>
    /// <param name="names">The names of the path, the top-level group first.</param>
    /// <returns>The identifier of the last group of the path, or <see langword="null"/> when no name was given.</returns>
    public async Task<string> FindOrCreatePathAsync(IEnumerable<string> names)
    {
        string parentId = null;

        foreach (var name in (names ?? []).Select(ReportDesignNormalizer.Truncate).Where(name => !string.IsNullOrEmpty(name)))
        {
            var tree = await GetTreeAsync();
            var match = tree.GetChildren(parentId)
                .FirstOrDefault(group => string.Equals(group.DisplayText, name, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                match = new ReportGroup
                {
                    DisplayText = name,
                    ParentId = parentId,
                    CreatedUtc = _clock.UtcNow,
                };

                await _groups.CreateAsync(match);
                _changed[match.ItemId] = match;
            }

            parentId = match.ItemId;
        }

        return parentId;
    }

    /// <summary>
    /// Saves a group as it is, under its identifier, replacing the group stored with that identifier. Used by imports,
    /// which check the parents once every group is in.
    /// </summary>
    /// <param name="group">The group, with its identifier.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ImportAsync(ReportGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentException.ThrowIfNullOrEmpty(group.ItemId);

        group.ModifiedUtc = _clock.UtcNow;

        if (await FindAsync(group.ItemId) is null)
        {
            await _groups.CreateAsync(group);
        }
        else
        {
            await _groups.UpdateAsync(group);
        }

        _changed[group.ItemId] = group;
    }

    /// <summary>
    /// Splits a group path written as text, such as <c>Sales / Regional</c>, into its names.
    /// </summary>
    /// <param name="path">The path text.</param>
    /// <returns>The names, the top-level group first.</returns>
    public static IReadOnlyList<string> SplitPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? []
            : path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

/// <summary>
/// The outcome of saving or deleting a report group.
/// </summary>
public sealed class ReportGroupResult
{
    /// <summary>
    /// Gets or sets the group.
    /// </summary>
    public ReportGroup Group { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the group was saved or deleted.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets the problems that prevented it.
    /// </summary>
    public IList<string> Errors { get; } = [];
}
