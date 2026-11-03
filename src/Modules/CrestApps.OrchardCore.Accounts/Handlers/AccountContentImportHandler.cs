using System.Data;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Lists.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Accounts.Handlers;

/// <summary>
/// Adds an <c>Account</c> column to the import and export of the records that can belong to an account, such as contacts. On import the column names
/// an account by its title and places the record in it; a name that matches no account, or more than one, leaves the
/// record where it was. On export it carries the account's title.
/// </summary>
internal sealed class AccountContentImportHandler : ContentImportHandlerBase, IContentImportHandler
{
    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IAccountContentTypeProvider _accountContentTypeProvider;
    private readonly Dictionary<string, ContentItem> _accountsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _namesById = new(StringComparer.Ordinal);

    private ImportColumn _accountColumn;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountContentImportHandler"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="accountContentTypeProvider">The provider that tells which types are accounts.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AccountContentImportHandler(
        ISession session,
        IContentManager contentManager,
        IAccountContentTypeProvider accountContentTypeProvider,
        IStringLocalizer<AccountContentImportHandler> stringLocalizer)
    {
        _session = session;
        _contentManager = contentManager;
        _accountContentTypeProvider = accountContentTypeProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentContext context)
    {
        if (!_accountContentTypeProvider.IsAccountChildType(context?.ContentTypeDefinition))
        {
            return [];
        }

        _accountColumn ??= new ImportColumn
        {
            Name = "Account",
            Description = S["The name of the account the record belongs to."],
            AdditionalNames = ["Account Name", "AccountName"],
        };

        return [_accountColumn];
    }

    /// <inheritdoc/>
    public async Task ImportAsync(ContentImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (GetColumns(context).Count == 0)
        {
            return;
        }

        string name = null;

        foreach (DataColumn column in context.Columns)
        {
            if (Is(column.ColumnName, _accountColumn))
            {
                name = context.Row[column]?.ToString()?.Trim();
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var account = await FindAccountAsync(name);

        if (account is null)
        {
            return;
        }

        context.ContentItem.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = account.ContentItemId;
            part.ListContentType = account.ContentType;
        });
    }

    /// <inheritdoc/>
    public async Task ExportAsync(ContentExportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (GetColumns(context).Count == 0 ||
            !context.ContentItem.TryGet<ContainedPart>(out var contained) ||
            string.IsNullOrEmpty(contained.ListContentItemId) ||
            !await _accountContentTypeProvider.IsAccountTypeAsync(contained.ListContentType))
        {
            return;
        }

        if (!_namesById.TryGetValue(contained.ListContentItemId, out var name))
        {
            name = (await _contentManager.GetAsync(contained.ListContentItemId, VersionOptions.Latest))?.DisplayText;
            _namesById[contained.ListContentItemId] = name;
        }

        context.Row[_accountColumn.Name] = name;
    }

    private async Task<ContentItem> FindAccountAsync(string name)
    {
        if (_accountsByName.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var accountTypes = (await _accountContentTypeProvider.GetAccountTypesAsync()).Select(type => type.Name).ToArray();
        ContentItem account = null;

        if (accountTypes.Length > 0)
        {
            var matches = (await _session.Query<ContentItem, ContentItemIndex>(index =>
                    index.Latest &&
                    index.ContentType.IsIn(accountTypes) &&
                    index.DisplayText == name)
                .Take(2)
                .ListAsync())
                .ToList();

            // Two accounts with one name cannot be told apart, so the record is left where it was.
            account = matches.Count == 1 ? matches[0] : null;
        }

        _accountsByName[name] = account;

        return account;
    }
}
