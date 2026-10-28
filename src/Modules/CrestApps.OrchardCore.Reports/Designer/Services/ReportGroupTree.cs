using CrestApps.OrchardCore.Reports.Designer.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// The report groups arranged as a tree: each group's children in display order, its path from the top level, and
/// which groups are nested in which. A group whose parent is missing, or whose parents lead back to itself (which only
/// an imported recipe can cause), is treated as a top-level group, so the tree never loops.
/// </summary>
public sealed class ReportGroupTree
{
    /// <summary>
    /// The text between the names of a group's path, such as <c>Sales / Regional</c>.
    /// </summary>
    public const string PathSeparator = " / ";

    private readonly Dictionary<string, ReportGroup> _groups;
    private readonly Dictionary<string, string> _parents;
    private readonly Dictionary<string, List<ReportGroup>> _children;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportGroupTree"/> class.
    /// </summary>
    /// <param name="groups">Every report group.</param>
    public ReportGroupTree(IEnumerable<ReportGroup> groups)
    {
        _groups = (groups ?? [])
            .Where(group => group is not null && !string.IsNullOrEmpty(group.ItemId))
            .GroupBy(group => group.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        _parents = new Dictionary<string, string>(StringComparer.Ordinal);
        _children = new Dictionary<string, List<ReportGroup>>(StringComparer.Ordinal);

        foreach (var group in _groups.Values)
        {
            _parents[group.ItemId] = LeadsBackToItself(group) ? null : Find(group.ParentId)?.ItemId;
        }

        foreach (var group in _groups.Values)
        {
            var key = _parents[group.ItemId] ?? string.Empty;

            if (!_children.TryGetValue(key, out var siblings))
            {
                _children[key] = siblings = [];
            }

            siblings.Add(group);
        }

        foreach (var siblings in _children.Values)
        {
            siblings.Sort(Compare);
        }
    }

    /// <summary>
    /// Gets the number of groups.
    /// </summary>
    public int Count => _groups.Count;

    /// <summary>
    /// Finds a group.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <returns>The group, or <see langword="null"/> when there is no group with that identifier.</returns>
    public ReportGroup Find(string id)
    {
        return !string.IsNullOrEmpty(id) && _groups.TryGetValue(id, out var group) ? group : null;
    }

    /// <summary>
    /// Gets the identifier of the group a group is nested in, as the tree places it.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <returns>The parent identifier, or <see langword="null"/> for a top-level or unknown group.</returns>
    public string GetParentId(string id)
    {
        return !string.IsNullOrEmpty(id) && _parents.TryGetValue(id, out var parentId) ? parentId : null;
    }

    /// <summary>
    /// Gets the groups nested directly in a group, in display order.
    /// </summary>
    /// <param name="parentId">The parent identifier, or <see langword="null"/> for the top-level groups.</param>
    /// <returns>The child groups.</returns>
    public IReadOnlyList<ReportGroup> GetChildren(string parentId)
    {
        return _children.TryGetValue(parentId ?? string.Empty, out var children) ? children : [];
    }

    /// <summary>
    /// Gets a group's path: the top-level group it is nested in first, the group itself last.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <returns>The groups of the path, empty for an unknown group.</returns>
    public IReadOnlyList<ReportGroup> GetPath(string id)
    {
        var path = new List<ReportGroup>();

        for (var group = Find(id); group is not null; group = Find(GetParentId(group.ItemId)))
        {
            path.Insert(0, group);
        }

        return path;
    }

    /// <summary>
    /// Gets the names of a group's path, such as <c>Sales / Regional</c>.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <returns>The path text, or <see langword="null"/> for an unknown group.</returns>
    public string GetPathText(string id)
    {
        var path = GetPath(id);

        return path.Count == 0 ? null : string.Join(PathSeparator, path.Select(group => group.DisplayText));
    }

    /// <summary>
    /// Determines whether a group is another group or nested in it, at any depth. A group cannot be moved into such a
    /// group, or it would be its own ancestor.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <param name="ancestorId">The identifier of the possible ancestor.</param>
    /// <returns><see langword="true"/> when <paramref name="id"/> is <paramref name="ancestorId"/> or nested in it.</returns>
    public bool IsSelfOrDescendant(string id, string ancestorId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(ancestorId))
        {
            return false;
        }

        return GetPath(id).Any(group => string.Equals(group.ItemId, ancestorId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Lists every group depth first, each group followed by the groups nested in it, in display order.
    /// </summary>
    /// <returns>The groups with their depth, <c>0</c> for a top-level group.</returns>
    public IReadOnlyList<ReportGroupTreeNode> Flatten()
    {
        var nodes = new List<ReportGroupTreeNode>(_groups.Count);

        void Add(string parentId, int depth)
        {
            foreach (var group in GetChildren(parentId))
            {
                nodes.Add(new ReportGroupTreeNode(group, depth, GetPathText(group.ItemId)));
                Add(group.ItemId, depth + 1);
            }
        }

        Add(null, 0);

        return nodes;
    }

    /// <summary>
    /// Orders sibling groups: by <see cref="ReportGroup.Order"/>, then by name.
    /// </summary>
    /// <param name="first">The first group.</param>
    /// <param name="second">The second group.</param>
    /// <returns>A negative number when <paramref name="first"/> comes first.</returns>
    public static int Compare(ReportGroup first, ReportGroup second)
    {
        var byOrder = first.Order.CompareTo(second.Order);

        return byOrder != 0 ? byOrder : StringComparer.CurrentCultureIgnoreCase.Compare(first.DisplayText, second.DisplayText);
    }

    private bool LeadsBackToItself(ReportGroup group)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { group.ItemId };

        for (var parent = Find(group.ParentId); parent is not null; parent = Find(parent.ParentId))
        {
            if (string.Equals(parent.ItemId, group.ItemId, StringComparison.Ordinal))
            {
                return true;
            }

            // A loop above the group, among its parents, does not include it: the group stays under its parent, and
            // the groups of the loop are the ones treated as top-level groups.
            if (!visited.Add(parent.ItemId))
            {
                return false;
            }
        }

        return false;
    }
}

/// <summary>
/// A report group in the order of <see cref="ReportGroupTree.Flatten"/>.
/// </summary>
/// <param name="Group">The group.</param>
/// <param name="Depth">How deep the group is nested, <c>0</c> for a top-level group.</param>
/// <param name="PathText">The names of the group's path, such as <c>Sales / Regional</c>.</param>
public sealed record ReportGroupTreeNode(ReportGroup Group, int Depth, string PathText);
