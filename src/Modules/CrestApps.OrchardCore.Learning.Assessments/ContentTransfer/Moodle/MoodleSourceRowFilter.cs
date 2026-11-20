using System.Security.Claims;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Core.Moodle;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Users;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Moodle;

/// <summary>
/// Makes importing a Moodle course again update what the first import made: a row whose course or activity was imported
/// before (by its source identifier, such as <c>moodle:1a2b3c4d5e6f:course:12:cm:345</c>) gets that item's id in the
/// <c>ContentItemId</c> column, which the import matches existing items by, when the item is of the row's type and the
/// person who queued the import may edit it.
/// </summary>
internal sealed class MoodleSourceRowFilter : IContentImportRowFilter
{
    private const string ContentItemIdColumn = "ContentItemId";

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IContentTransferFileFormatProvider> _formats;
    private readonly IServiceProvider _services;
    private readonly MoodleWorkspaces _workspaces;

    private ContentTransferEntry _entry;
    private ClaimsPrincipal _importer;
    private bool _importerRead;

    public MoodleSourceRowFilter(
        ISession session,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IEnumerable<IContentTransferFileFormatProvider> formats,
        IServiceProvider services,
        MoodleWorkspaces workspaces,
        IStringLocalizer<MoodleSourceRowFilter> stringLocalizer)
    {
        _session = session;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _formats = formats;
        _services = services;
        _workspaces = workspaces;
        S = stringLocalizer;
    }

    internal readonly IStringLocalizer S;

    /// <inheritdoc />
    public Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _entry = context.Entry;

        return Task.FromResult(MoodleImportHandler.ReadByMoodle(_formats, context.Entry));
    }

    /// <inheritdoc />
    public async Task PrepareBatchAsync(ContentImportRowFilterBatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rows = context.Rows
            .Where(row => row.Columns.Contains(MoodleColumns.Source) && row.Columns.Contains(ContentItemIdColumn))
            .Select(row => (row.Row, Source: LearningSourceIndexProvider.Truncate(row.Row[MoodleColumns.Source]?.ToString()?.Trim())))
            .Where(row => !string.IsNullOrEmpty(row.Source) && string.IsNullOrWhiteSpace(row.Row[ContentItemIdColumn]?.ToString()))
            .ToList();

        if (rows.Count == 0 || await ImporterAsync() is not { } importer)
        {
            return;
        }

        var sources = rows.Select(row => row.Source).Distinct(StringComparer.Ordinal).ToArray();
        var matches = await _session.QueryIndex<LearningSourceIndex>(index => index.SourceIdentifier.IsIn(sources)).ListAsync(context.CancellationToken);

        foreach (var (row, source) in rows)
        {
            var type = row.Table.Columns.Contains(ContentTransferColumns.ContentType) ? row[ContentTransferColumns.ContentType]?.ToString() : null;

            foreach (var match in matches.Where(match => match.SourceIdentifier == source && (string.IsNullOrEmpty(type) || match.ContentType == type)).OrderBy(match => match.Id))
            {
                var item = await _contentManager.GetAsync(match.ContentItemId, VersionOptions.Latest);

                if (item is null || !await _authorizationService.AuthorizeAsync(importer, CommonPermissions.EditContent, item))
                {
                    continue;
                }

                row[ContentItemIdColumn] = item.ContentItemId;

                break;
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A cohort sync row: never imported as an item. The course row links the cohorts the site has; a cohort it does
        // not have, or cohorts turned off, is reported here.
        if (context.Columns is null || !context.Columns.Contains(MoodleColumns.Source) || !context.Columns.Contains(MoodleColumns.Workspace) ||
            _workspaces.Find(context.Row[MoodleColumns.Workspace]?.ToString()) is not { } workspace ||
            MoodleCourseRows.FindCohortSync(workspace.Backup, context.Row[MoodleColumns.Source]?.ToString()) is not { } cohortSync)
        {
            return false;
        }

        var backup = workspace.Backup;
        var title = string.IsNullOrWhiteSpace(backup.Course.FullName) ? backup.Course.ShortName : backup.Course.FullName;
        var name = MoodleCourseRows.CohortName(cohortSync);

        if (_services.GetService<ICohortService>() is not { } cohorts || !_services.GetServices<ICohortLearningTarget>().Any())
        {
            context.SkipReason = S["{0}: Moodle enrols learners by the cohort \"{1}\"; turn on Cohorts and Enrollment by Cohort and import the backup again.", title, name];
        }
        else if (await ImporterAsync() is not { } importer ||
            !await _authorizationService.AuthorizeAsync(importer, LearningPermissions.ManageEnrollments) ||
            MoodleCohorts.Match(cohortSync, (await cohorts.ListAsync(10_000)).Select(cohort => (cohort.CohortId, cohort.Name))) is null)
        {
            // One message whether the cohort exists or the importer may not link it: the report never tells which
            // cohorts the site has to someone who may not manage enrollments.
            context.SkipReason = S["{0}: Moodle enrols learners by the cohort \"{1}\"; it is not linked to the course. A cohort is linked only when the site has a cohort of the same id number or name (Access Control > Cohorts) and the person importing may manage enrollments; then import the backup again.", title, name];
        }

        if (context.SkipReason is not null)
        {
            context.SkipCategory = S["Not imported from Moodle"];
            context.SkipReasonCode = "MoodleCohortNotFound";
        }

        return true;
    }

    // The person who queued the import: the background import runs without a request.
    private async Task<ClaimsPrincipal> ImporterAsync()
    {
        if (_importerRead)
        {
            return _importer;
        }

        _importerRead = true;

        return _importer = await LearningUserPrincipals.CreateAsync(_services, _entry?.Owner);
    }
}

/// <summary>
/// The rows of a Moodle backup imported as a course: the course and the items it holds.
/// </summary>
internal sealed class MoodleRowTypeProvider : IContentImportRowTypeProvider
{
    private readonly CourseItemKindOptions _kinds;

    public MoodleRowTypeProvider(IOptions<CourseItemKindOptions> kinds)
    {
        _kinds = kinds.Value;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetRowContentTypes(ContentTypeDefinition contentType)
        => contentType?.Name == LearningConstants.ContentTypes.Course
            ? MoodleCourseRows.ItemTypes
                .Where(type => _kinds.Find(MoodleCourseRows.ItemKinds[type.Key]) is not null)
                .Select(type => type.Value)
                .Prepend(LearningConstants.ContentTypes.Course)
                .Distinct(StringComparer.Ordinal)
            : [];
}
