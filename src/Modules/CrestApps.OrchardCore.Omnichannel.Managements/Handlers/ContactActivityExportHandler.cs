using System.Data;
using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Appends each exported contact's most recent completed activity of a chosen subject to the content
/// transfer bulk export: the agent note, completion date, the completing user, the disposition, the subject,
/// and all of the subject's fields. The option is contributed by <c>OmnichannelActivityExportDisplayDriver</c>
/// and stored on the export entry as an <see cref="OmnichannelActivityExportPart"/>. All columns are export-only.
/// </summary>
public sealed class ContactActivityExportHandler : IContentImportHandler, IContentExportBatchHandler
{
    private const string NoteColumn = "LastActivityNote";
    private const string CompletedColumn = "LastActivityCompletedUtc";
    private const string CompletedByColumn = "LastActivityCompletedBy";
    private const string DispositionColumn = "LastActivityDisposition";
    private const string SubjectColumn = "LastActivitySubject";

    // Only used to disambiguate a subject field whose column name would collide with a contact column
    // (for example a shared part such as TitlePart). Type-specific subject fields are already prefixed with
    // the subject content type name, so they keep their own names.
    private const string SubjectCollisionPrefix = "Subject";

    // The generic content-item metadata columns are not useful for the subject snapshot, so they are dropped.
    private static readonly HashSet<string> _excludedSubjectColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ContentItem.ContentItemId),
        nameof(ContentItem.ContentItemVersionId),
        nameof(ContentItem.CreatedUtc),
        nameof(ContentItem.ModifiedUtc),
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly INamedCatalog<OmnichannelDisposition> _dispositionCatalog;
    private readonly IDisplayNameProvider _displayNameProvider;

    // Cached per active subject content type within the export scope.
    private string _subjectColumnsFor;
    private IReadOnlyList<KeyValuePair<string, string>> _subjectColumnMap;
    private string _subjectTypeDisplayName;
    private string _subjectTypeDisplayNameFor;
    private Dictionary<string, string> _dispositionNames;

    // Each contact's latest completed activity for the current export page, pre-loaded in one query, plus the
    // display names of the users who completed them. Non-null once a page has been prepared.
    private Dictionary<string, OmnichannelActivity> _activitiesByContact;
    private Dictionary<string, string> _completedByNames;

    public ContactActivityExportHandler(
        IServiceProvider serviceProvider,
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        INamedCatalog<OmnichannelDisposition> dispositionCatalog,
        IDisplayNameProvider displayNameProvider)
    {
        _serviceProvider = serviceProvider;
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _dispositionCatalog = dispositionCatalog;
        _displayNameProvider = displayNameProvider;
    }

    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentContext context)
    {
        if (!TryGetOptions(context.Entry, out var part))
        {
            return [];
        }

        var columns = new List<ImportColumn>
        {
            ExportColumn(NoteColumn),
            ExportColumn(CompletedColumn),
            ExportColumn(CompletedByColumn),
            ExportColumn(DispositionColumn),
            ExportColumn(SubjectColumn),
        };

        foreach (var mapping in GetSubjectColumnMap(part.SubjectContentType, context.ContentTypeDefinition))
        {
            columns.Add(ExportColumn(mapping.Value));
        }

        return columns;
    }

    public Task ImportAsync(ContentImportContext content)
    {
        // Export-only handler.
        return Task.CompletedTask;
    }

    public async Task PrepareExportBatchAsync(ContentExportBatchContext context)
    {
        if (!TryGetOptions(context.Entry, out var part))
        {
            _activitiesByContact = null;
            _completedByNames = null;

            return;
        }

        var contactIds = context.ContentItems
            .Select(item => item.ContentItemId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (contactIds.Length == 0)
        {
            _activitiesByContact = new Dictionary<string, OmnichannelActivity>(StringComparer.Ordinal);
            _completedByNames = new Dictionary<string, string>(StringComparer.Ordinal);

            return;
        }

        // Load every completed activity of the subject for the whole page in one query, then keep the most
        // recent per contact. This replaces the previous one-query-per-contact lookup.
        var activities = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.ContactContentItemId.IsIn(contactIds)
                    && index.SubjectContentType == part.SubjectContentType
                    && index.Status == ActivityStatus.Completed,
                collection: OmnichannelConstants.CollectionName)
            .ListAsync();

        _activitiesByContact = activities
            .Where(activity => !string.IsNullOrEmpty(activity.ContactContentItemId))
            .GroupBy(activity => activity.ContactContentItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(activity => activity.CompletedUtc).First(),
                StringComparer.Ordinal);

        // Resolve the display names of the users who completed these activities in a single query.
        _completedByNames = await ResolveUserDisplayNamesAsync(
            _activitiesByContact.Values
                .Select(activity => activity.CompletedById)
                .Where(id => !string.IsNullOrEmpty(id)));
    }

    public async Task ExportAsync(ContentExportContext content)
    {
        if (!TryGetOptions(content.Entry, out var part))
        {
            return;
        }

        var contactId = content.ContentItem?.ContentItemId;

        if (string.IsNullOrEmpty(contactId))
        {
            content.Exclude = part.OnlyContactsWithLastActivity;

            return;
        }

        var activity = await ResolveLastActivityAsync(contactId, part.SubjectContentType);

        if (activity is null)
        {
            // Drop contacts without a matching activity when the user asked for only those that have one.
            content.Exclude = part.OnlyContactsWithLastActivity;

            return;
        }

        SetCell(content.Row, NoteColumn, activity.Notes);

        if (activity.CompletedUtc.HasValue)
        {
            SetCell(content.Row, CompletedColumn, activity.CompletedUtc.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        SetCell(content.Row, CompletedByColumn, await ResolveCompletedByAsync(activity));
        SetCell(content.Row, DispositionColumn, await ResolveDispositionAsync(activity.DispositionId));

        // The subject snapshot on the activity is never persisted, so it has no display text; fall back to the
        // subject content type's title.
        var subjectDisplay = !string.IsNullOrWhiteSpace(activity.Subject?.DisplayText)
            ? activity.Subject.DisplayText
            : await GetSubjectTypeDisplayNameAsync(part.SubjectContentType);

        SetCell(content.Row, SubjectColumn, subjectDisplay);

        if (activity.Subject is not null)
        {
            await ExportSubjectFieldsAsync(content, part.SubjectContentType, activity.Subject);
        }
    }

    private async Task ExportSubjectFieldsAsync(ContentExportContext content, string subjectContentType, ContentItem subject)
    {
        var map = GetSubjectColumnMap(subjectContentType, content.ContentTypeDefinition);

        if (map.Count == 0)
        {
            return;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        if (definition is null)
        {
            return;
        }

        var importManager = _serviceProvider.GetRequiredService<IContentImportManager>();

        using var table = new DataTable();

        foreach (var mapping in map)
        {
            table.Columns.Add(mapping.Key);
        }

        var subjectContext = new ContentExportContext
        {
            ContentItem = subject,
            ContentTypeDefinition = definition,
            Row = table.NewRow(),
        };

        // Entry is intentionally left null so this handler does not re-enter itself for the subject item.
        await importManager.ExportAsync(subjectContext);

        foreach (var mapping in map)
        {
            SetCell(content.Row, mapping.Value, subjectContext.Row[mapping.Key]?.ToString());
        }
    }

    private async Task<OmnichannelActivity> ResolveLastActivityAsync(string contactId, string subjectContentType)
    {
        // Prefer the per-page batch that PrepareExportBatchAsync loaded; a non-null map means the page was
        // prepared, so an absent contact simply has no matching activity.
        if (_activitiesByContact is not null)
        {
            return _activitiesByContact.GetValueOrDefault(contactId);
        }

        // Fallback for callers that map a row without preparing the page first.
        return await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.ContactContentItemId == contactId
                    && index.SubjectContentType == subjectContentType
                    && index.Status == ActivityStatus.Completed,
                collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(index => index.CompletedUtc)
            .FirstOrDefaultAsync();
    }

    private async Task<string> ResolveCompletedByAsync(OmnichannelActivity activity)
    {
        if (string.IsNullOrEmpty(activity.CompletedById))
        {
            return activity.CompletedByUsername ?? string.Empty;
        }

        if (_completedByNames is not null && _completedByNames.TryGetValue(activity.CompletedById, out var cached))
        {
            return cached;
        }

        // Fallback for callers that map a row without preparing the page first.
        var names = await ResolveUserDisplayNamesAsync([activity.CompletedById]);

        return names.TryGetValue(activity.CompletedById, out var name)
            ? name
            : activity.CompletedByUsername ?? activity.CompletedById;
    }

    private async Task<Dictionary<string, string>> ResolveUserDisplayNamesAsync(IEnumerable<string> userIds)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (ids.Length == 0)
        {
            return names;
        }

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(ids)).ListAsync();

        foreach (var user in users)
        {
            names[user.UserId] = await _displayNameProvider.GetAsync(user);
        }

        return names;
    }

    private async Task<string> GetSubjectTypeDisplayNameAsync(string subjectContentType)
    {
        if (_subjectTypeDisplayName is not null && string.Equals(_subjectTypeDisplayNameFor, subjectContentType, StringComparison.Ordinal))
        {
            return _subjectTypeDisplayName;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        _subjectTypeDisplayName = string.IsNullOrWhiteSpace(definition?.DisplayName) ? subjectContentType : definition.DisplayName;
        _subjectTypeDisplayNameFor = subjectContentType;

        return _subjectTypeDisplayName;
    }

    /// <summary>
    /// Builds the map of subject export columns as (source column name on the subject → output column name in
    /// the export). Type-specific subject fields keep their own names; a name that would collide with a
    /// contact column or a base last-activity column is prefixed to stay unique.
    /// </summary>
    private IReadOnlyList<KeyValuePair<string, string>> GetSubjectColumnMap(string subjectContentType, ContentTypeDefinition contactDefinition)
    {
        // The map depends on both types: the subject supplies the columns, the contact supplies the names to
        // avoid colliding with.
        var cacheKey = $"{contactDefinition?.Name}|{subjectContentType}";

        if (_subjectColumnMap is not null && string.Equals(_subjectColumnsFor, cacheKey, StringComparison.Ordinal))
        {
            return _subjectColumnMap;
        }

        // GetColumns is synchronous by contract; this runs on the export background thread which has no
        // synchronization context, so blocking here is safe. The result is cached for the export.
        var subjectDefinition = _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType).GetAwaiter().GetResult();

        if (subjectDefinition is null)
        {
            _subjectColumnMap = [];
            _subjectColumnsFor = cacheKey;

            return _subjectColumnMap;
        }

        var importManager = _serviceProvider.GetRequiredService<IContentImportManager>();

        var subjectColumns = importManager
            .GetColumnsAsync(new ImportContentContext { ContentTypeDefinition = subjectDefinition })
            .GetAwaiter().GetResult()
            .Where(column => column.Type != ImportColumnType.ImportOnly && !_excludedSubjectColumns.Contains(column.Name))
            .Select(column => column.Name);

        // Names already taken by the contact's own columns and the base last-activity columns; a subject
        // column that collides is prefixed so the export never emits duplicate column names.
        var taken = new HashSet<string>(GetContactColumnNames(contactDefinition, importManager), StringComparer.OrdinalIgnoreCase)
        {
            NoteColumn,
            CompletedColumn,
            CompletedByColumn,
            DispositionColumn,
            SubjectColumn,
        };

        var map = new List<KeyValuePair<string, string>>();

        foreach (var name in subjectColumns)
        {
            var output = taken.Contains(name) ? SubjectCollisionPrefix + name : name;
            taken.Add(output);
            map.Add(new KeyValuePair<string, string>(name, output));
        }

        _subjectColumnMap = map;
        _subjectColumnsFor = cacheKey;

        return _subjectColumnMap;
    }

    private static IEnumerable<string> GetContactColumnNames(ContentTypeDefinition contactDefinition, IContentImportManager importManager)
    {
        if (contactDefinition is null)
        {
            return [];
        }

        // Entry is intentionally omitted so this handler does not contribute (and re-enter) here.
        return importManager
            .GetColumnsAsync(new ImportContentContext { ContentTypeDefinition = contactDefinition })
            .GetAwaiter().GetResult()
            .Select(column => column.Name);
    }

    private async Task<string> ResolveDispositionAsync(string dispositionId)
    {
        if (string.IsNullOrEmpty(dispositionId))
        {
            return string.Empty;
        }

        _dispositionNames ??= (await _dispositionCatalog.GetAllAsync())
            .ToDictionary(disposition => disposition.ItemId, disposition => disposition.Name, StringComparer.Ordinal);

        return _dispositionNames.TryGetValue(dispositionId, out var name) ? name : dispositionId;
    }

    private static bool TryGetOptions(ContentTransferEntry entry, out OmnichannelActivityExportPart part)
    {
        part = null;

        if (entry is null || !entry.TryGet(out OmnichannelActivityExportPart stored))
        {
            return false;
        }

        if (!stored.IncludeLastActivity || string.IsNullOrEmpty(stored.SubjectContentType))
        {
            return false;
        }

        part = stored;

        return true;
    }

    private static void SetCell(DataRow row, string columnName, string value)
    {
        if (row.Table.Columns.Contains(columnName))
        {
            row[columnName] = value ?? string.Empty;
        }
    }

    private static ImportColumn ExportColumn(string name)
        => new()
        {
            Name = name,
            Type = ImportColumnType.ExportOnly,
        };
}
