using System.Data;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Imports and exports the lead columns: status by name, source by name, list, company, rating and owner by user name. A
/// file's own values win; where a row leaves a value empty, the list, source, status and owner chosen for the file
/// fill it. The conversion columns are exported only, because conversion is something that happens to a lead, not
/// something a file can claim.
/// </summary>
public sealed class LeadPartContentImportHandler : ContentImportHandlerBase, IContentPartImportHandler
{
    private readonly INamedCatalog<LeadStatus> _statuses;
    private readonly LeadSourceProvider _sources;
    private readonly LeadRatingProvider _ratings;
    private readonly UserManager<IUser> _userManager;
    private readonly ImportRowDoNotCallFlags _doNotCallFlags;
    private readonly IClock _clock;
    private readonly Dictionary<string, string> _userIdsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _userNamesById = new(StringComparer.Ordinal);

    private IReadOnlyList<LeadStatus> _cachedStatuses;

    private ImportColumn _statusColumn;
    private ImportColumn _sourceColumn;
    private ImportColumn _listColumn;
    private ImportColumn _companyColumn;
    private ImportColumn _ratingColumn;
    private ImportColumn _ownerColumn;
    private ImportColumn _isConvertedColumn;
    private ImportColumn _convertedUtcColumn;
    private ImportColumn _convertedContactColumn;
    private ImportColumn _lastScrubbedColumn;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadPartContentImportHandler"/> class.
    /// </summary>
    /// <param name="statuses">The lead status catalog.</param>
    /// <param name="sources">The lead sources, resolved by name.</param>
    /// <param name="ratings">The lead ratings, resolved by name.</param>
    /// <param name="userManager">The user manager used to resolve owners by user name.</param>
    /// <param name="doNotCallFlags">What the do-not-call screening decided about each row of the import.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadPartContentImportHandler(
        INamedCatalog<LeadStatus> statuses,
        LeadSourceProvider sources,
        LeadRatingProvider ratings,
        UserManager<IUser> userManager,
        ImportRowDoNotCallFlags doNotCallFlags,
        IClock clock,
        IStringLocalizer<LeadPartContentImportHandler> stringLocalizer)
    {
        _statuses = statuses;
        _sources = sources;
        _ratings = ratings;
        _userManager = userManager;
        _doNotCallFlags = doNotCallFlags;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentPartContext context)
    {
        _statusColumn ??= new ImportColumn
        {
            Name = "LeadStatus",
            Description = S["The name of the lead status, for example Working - Contacted."],
            AdditionalNames = ["Status", "Lead Status"],
        };

        _sourceColumn ??= new ImportColumn
        {
            Name = "LeadSource",
            Description = S["The name of the lead source, for example Trade show. A name that matches no lead source is ignored."],
            AdditionalNames = ["Source", "Lead Source"],
        };

        _listColumn ??= new ImportColumn
        {
            Name = "LeadList",
            Description = S["The list or import the lead arrived in."],
            AdditionalNames = ["List", "List Name", "ListName"],
        };

        _companyColumn ??= new ImportColumn
        {
            Name = "Company",
            Description = S["The company the lead works for."],
            AdditionalNames = ["Company Name", "CompanyName", "Organization"],
        };

        _ratingColumn ??= new ImportColumn
        {
            Name = "Rating",
            Description = S["One of the ratings of the lead Rating field, for example Hot, Warm or Cold."],
            AdditionalNames = ["Lead Rating"],
        };

        _ownerColumn ??= new ImportColumn
        {
            Name = "LeadOwner",
            Description = S["The user name of the user who owns the lead."],
            AdditionalNames = ["Owner", "Lead Owner"],
        };

        _isConvertedColumn ??= new ImportColumn
        {
            Name = "IsConverted",
            Description = S["Whether the lead was converted. Exported only."],
            Type = ImportColumnType.ExportOnly,
        };

        _convertedUtcColumn ??= new ImportColumn
        {
            Name = "ConvertedUtc",
            Description = S["When the lead was converted, in UTC. Exported only."],
            Type = ImportColumnType.ExportOnly,
        };

        _convertedContactColumn ??= new ImportColumn
        {
            Name = "ConvertedContactItemId",
            Description = S["The contact the lead became. Exported only."],
            Type = ImportColumnType.ExportOnly,
        };

        _lastScrubbedColumn ??= new ImportColumn
        {
            Name = "LastScrubbedUtc",
            Description = S["When the lead's numbers were last checked against a do-not-call registry, in UTC. Exported only."],
            Type = ImportColumnType.ExportOnly,
        };

        return
        [
            _statusColumn,
            _sourceColumn,
            _listColumn,
            _companyColumn,
            _ratingColumn,
            _ownerColumn,
            _isConvertedColumn,
            _convertedUtcColumn,
            _convertedContactColumn,
            _lastScrubbedColumn,
        ];
    }

    /// <inheritdoc/>
    public async Task ImportAsync(ContentPartImportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _ = GetColumns(context);

        var options = context.Entry?.GetOrCreate<OmnichannelContactImportOptionsPart>();
        var part = context.ContentItem.GetOrCreate<LeadPart>();

        // A converted lead is a closed record; a file cannot rewrite it.
        if (part.IsConverted)
        {
            return;
        }

        string status = null, source = null, list = null, company = null, rating = null, owner = null;

        foreach (DataColumn column in context.Columns)
        {
            var value = context.Row[column]?.ToString()?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (Is(column.ColumnName, _statusColumn))
            {
                status = value;
            }
            else if (Is(column.ColumnName, _sourceColumn))
            {
                source = value;
            }
            else if (Is(column.ColumnName, _listColumn))
            {
                list = value;
            }
            else if (Is(column.ColumnName, _companyColumn))
            {
                company = value;
            }
            else if (Is(column.ColumnName, _ratingColumn))
            {
                rating = value;
            }
            else if (Is(column.ColumnName, _ownerColumn))
            {
                owner = value;
            }
        }

        var statusId = await ResolveStatusIdAsync(status) ?? (string.IsNullOrEmpty(part.StatusId) ? options?.LeadStatusId : null);

        if (!string.IsNullOrEmpty(statusId))
        {
            part.StatusId = statusId;
        }

        var sourceId = await _sources.FindIdAsync(source) ?? part.Source.GetFirstContentItemId() ?? options?.LeadSourceId;
        var ownerId = await ResolveUserIdAsync(owner) ?? part.Owner.GetFirstUserId() ?? options?.LeadOwnerId;

        part.Source = new ContentPickerField
        {
            ContentItemIds = string.IsNullOrEmpty(sourceId) ? [] : [sourceId],
        };
        part.ListName = new TextField
        {
            Text = list ?? part.ListName.GetTrimmedText() ?? options?.LeadListName,
        };
        part.Company = new TextField
        {
            Text = company ?? part.Company.GetTrimmedText(),
        };
        part.Rating = new TextField
        {
            Text = await _ratings.NormalizeAsync(rating) ?? part.Rating.GetTrimmedText(),
        };
        part.Owner = new UserPickerField
        {
            UserIds = string.IsNullOrEmpty(ownerId) ? [] : [ownerId],
        };

        // The import checked this row's numbers against a do-not-call registry, so the lead records when.
        if (_doNotCallFlags.IsScreened(context.Row))
        {
            part.LastScrubbedUtc = _clock.UtcNow;
        }

        LeadImports.Record(part, context.Entry);

        context.ContentItem.Apply(part);
    }

    /// <inheritdoc/>
    public async Task ExportAsync(ContentPartExportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.ContentItem.TryGet<LeadPart>(out var part))
        {
            return;
        }

        _ = GetColumns(null);

        var statuses = await GetStatusesAsync();

        context.Row[_statusColumn.Name] = statuses.FirstOrDefault(entry => entry.ItemId == part.StatusId)?.Name;
        context.Row[_sourceColumn.Name] = await _sources.GetNameAsync(part.Source.GetFirstContentItemId());
        context.Row[_listColumn.Name] = part.ListName.GetTrimmedText();
        context.Row[_companyColumn.Name] = part.Company.GetTrimmedText();
        context.Row[_ratingColumn.Name] = part.Rating.GetTrimmedText();
        context.Row[_ownerColumn.Name] = await ResolveUserNameAsync(part.Owner.GetFirstUserId());
        context.Row[_isConvertedColumn.Name] = part.IsConverted;

        if (part.ConvertedUtc.HasValue)
        {
            context.Row[_convertedUtcColumn.Name] = part.ConvertedUtc.Value;
        }

        context.Row[_convertedContactColumn.Name] = part.ConvertedContactItemId;

        if (part.LastScrubbedUtc.HasValue)
        {
            context.Row[_lastScrubbedColumn.Name] = part.LastScrubbedUtc.Value;
        }
    }

    private async Task<string> ResolveStatusIdAsync(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // The converted status is never taken from a file: only converting a lead gives it that status.
        var status = (await GetStatusesAsync()).FirstOrDefault(entry =>
            !entry.IsConverted &&
            (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase) || entry.ItemId == name));

        return status?.ItemId;
    }

    private async Task<string> ResolveUserIdAsync(string userName)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return null;
        }

        if (!_userIdsByName.TryGetValue(userName, out var userId))
        {
            userId = await _userManager.FindByNameAsync(userName) is User user ? user.UserId : null;
            _userIdsByName[userName] = userId;
        }

        return userId;
    }

    private async Task<string> ResolveUserNameAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        if (!_userNamesById.TryGetValue(userId, out var userName))
        {
            userName = await _userManager.FindByIdAsync(userId) is User user ? user.UserName : null;
            _userNamesById[userId] = userName;
        }

        return userName;
    }

    private async Task<IReadOnlyList<LeadStatus>> GetStatusesAsync()
        => _cachedStatuses ??= (await _statuses.GetAllAsync()).ToArray();
}
