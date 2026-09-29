using System.Data;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Imports and exports the lead columns: status by name, source, list, company, rating and owner by user name. A
/// file's own values win; where a row leaves a value empty, the list, source, status and owner chosen for the file
/// fill it. The conversion columns are exported only, because conversion is something that happens to a lead, not
/// something a file can claim.
/// </summary>
public sealed class LeadPartContentImportHandler : ContentImportHandlerBase, IContentPartImportHandler
{
    private readonly INamedCatalog<LeadStatus> _statuses;
    private readonly UserManager<IUser> _userManager;
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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadPartContentImportHandler"/> class.
    /// </summary>
    /// <param name="statuses">The lead status catalog.</param>
    /// <param name="userManager">The user manager used to resolve owners by user name.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadPartContentImportHandler(
        INamedCatalog<LeadStatus> statuses,
        UserManager<IUser> userManager,
        IStringLocalizer<LeadPartContentImportHandler> stringLocalizer)
    {
        _statuses = statuses;
        _userManager = userManager;
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
            Description = S["Where the lead came from."],
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
            Description = S["Hot, Warm or Cold."],
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

        part.Source = source ?? part.Source ?? options?.LeadSource;
        part.ListName = list ?? part.ListName ?? options?.LeadListName;
        part.Company = company ?? part.Company;
        part.Rating = LeadRatings.Normalize(rating) ?? part.Rating;
        part.OwnerId = await ResolveUserIdAsync(owner) ?? part.OwnerId ?? options?.LeadOwnerId;

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
        context.Row[_sourceColumn.Name] = part.Source;
        context.Row[_listColumn.Name] = part.ListName;
        context.Row[_companyColumn.Name] = part.Company;
        context.Row[_ratingColumn.Name] = part.Rating;
        context.Row[_ownerColumn.Name] = await ResolveUserNameAsync(part.OwnerId);
        context.Row[_isConvertedColumn.Name] = part.IsConverted;

        if (part.ConvertedUtc.HasValue)
        {
            context.Row[_convertedUtcColumn.Name] = part.ConvertedUtc.Value;
        }

        context.Row[_convertedContactColumn.Name] = part.ConvertedContactItemId;
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
