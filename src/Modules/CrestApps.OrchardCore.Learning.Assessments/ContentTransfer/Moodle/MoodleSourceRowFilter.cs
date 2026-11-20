using System.Security.Claims;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
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

    private ContentTransferEntry _entry;
    private ClaimsPrincipal _importer;
    private bool _importerRead;

    public MoodleSourceRowFilter(
        ISession session,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IEnumerable<IContentTransferFileFormatProvider> formats,
        IServiceProvider services)
    {
        _session = session;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _formats = formats;
        _services = services;
    }

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
    public Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
        => Task.FromResult(false);

    // The person who queued the import: the background import runs without a request.
    private async Task<ClaimsPrincipal> ImporterAsync()
    {
        if (_importerRead)
        {
            return _importer;
        }

        _importerRead = true;

        if (string.IsNullOrEmpty(_entry?.Owner) ||
            _services.GetService<UserManager<IUser>>() is not { } users ||
            _services.GetService<IUserClaimsPrincipalFactory<IUser>>() is not { } principals ||
            await users.FindByIdAsync(_entry.Owner) is not { } user)
        {
            return null;
        }

        return _importer = await principals.CreateAsync(user);
    }
}

/// <summary>
/// The rows of a Moodle backup imported as a course: the course and the items it holds.
/// </summary>
internal sealed class MoodleRowTypeProvider : IContentImportRowTypeProvider
{
    /// <inheritdoc />
    public IEnumerable<string> GetRowContentTypes(ContentTypeDefinition contentType)
        => contentType?.Name == LearningConstants.ContentTypes.Course
            ? MoodleCourseRows.ItemTypes.Values.Prepend(LearningConstants.ContentTypes.Course).Distinct(StringComparer.Ordinal)
            : [];
}
