using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Reports.Designer.Migrations;

/// <summary>
/// Turns the free-text category of the reports saved before report groups into groups: each distinct category becomes
/// a top-level group of that name (categories that differ only in case share one), and every report, draft, and version
/// with that category is placed in it.
/// </summary>
internal sealed class ReportGroupMigrations : DataMigration
{
    private readonly ICatalog<ReportDesign> _designs;
    private readonly ReportGroupService _groupService;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportGroupMigrations"/> class.
    /// </summary>
    /// <param name="designs">The report catalog.</param>
    /// <param name="groupService">The group service, which finds or creates the group of a category.</param>
    /// <param name="session">The session that reads and saves drafts and versions.</param>
    public ReportGroupMigrations(
        ICatalog<ReportDesign> designs,
        ReportGroupService groupService,
        ISession session)
    {
        _designs = designs;
        _groupService = groupService;
        _session = session;
    }

    /// <summary>
    /// Moves the categories of the stored reports, drafts, and versions into groups.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        var groupIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var design in await _designs.GetAllAsync())
        {
            if (await MoveCategoryAsync(design, groupIds))
            {
                await _designs.UpdateAsync(design);
            }
        }

        foreach (var draft in await _session.Query<ReportDesignDraft>().ListAsync())
        {
            if (await MoveCategoryAsync(draft.Design, groupIds))
            {
                await _session.SaveAsync(draft);
            }
        }

        foreach (var version in await _session.Query<ReportDesignVersion>().ListAsync())
        {
            if (await MoveCategoryAsync(version.Design, groupIds))
            {
                await _session.SaveAsync(version);
            }
        }

        return 1;
    }

    // Places a report with a category in the group of that name, unless it already has a group, and clears the category.
    private async Task<bool> MoveCategoryAsync(ReportDesign design, Dictionary<string, string> groupIds)
    {
#pragma warning disable CS0618 // Category is read only to turn it into a group.
        var category = ReportDesignNormalizer.Truncate(design?.Category);

        if (category is null)
        {
            return false;
        }

        design.Category = null;
#pragma warning restore CS0618

        if (string.IsNullOrEmpty(design.GroupId))
        {
            if (!groupIds.TryGetValue(category, out var groupId))
            {
                groupIds[category] = groupId = await _groupService.FindOrCreatePathAsync([category]);
            }

            design.GroupId = groupId;
        }

        return true;
    }
}
