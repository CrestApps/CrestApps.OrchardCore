using CrestApps.OrchardCore.Accounts.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Lists.Models;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Keeps the list of every account type offering the types that can belong to an account. A type is added to an
/// account's list once, the first time it is seen, and recorded as offered, so an administrator who removes it from
/// the list keeps it removed. A type that must not belong to an account is taken out of the list.
/// </summary>
public sealed class AccountListSynchronizer
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IAccountContentTypeProvider _accountContentTypeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountListSynchronizer"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="accountContentTypeProvider">The provider that tells which types can belong to an account.</param>
    /// <param name="logger">The logger.</param>
    public AccountListSynchronizer(
        IContentDefinitionManager contentDefinitionManager,
        IAccountContentTypeProvider accountContentTypeProvider,
        ILogger<AccountListSynchronizer> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _accountContentTypeProvider = accountContentTypeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the technical names of the types that can belong to an account, in the order they are defined.
    /// </summary>
    public async Task<string[]> GetChildTypesAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(_accountContentTypeProvider.IsAccountChildType)
            .Select(definition => definition.Name)
            .ToArray();

    /// <summary>
    /// Brings every account type's list up to date with the current content types.
    /// </summary>
    public async Task SynchronizeAsync()
    {
        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        var childTypes = definitions
            .Where(_accountContentTypeProvider.IsAccountChildType)
            .Select(definition => definition.Name)
            .ToArray();

        var excludedTypes = definitions
            .Where(_accountContentTypeProvider.IsExcludedFromAccounts)
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var accountType in definitions.Where(AccountRecordKinds.IsAccount))
        {
            await SynchronizeAsync(accountType, childTypes, excludedTypes);
        }
    }

    private async Task SynchronizeAsync(ContentTypeDefinition accountType, string[] childTypes, HashSet<string> excludedTypes)
    {
        var listPart = accountType.Parts.FirstOrDefault(part => part.PartDefinition?.Name == AccountsConstants.ContentParts.List);
        var accountPart = accountType.Parts.FirstOrDefault(part => part.PartDefinition?.Name == AccountsConstants.ContentParts.Account);

        if (listPart is null || accountPart is null)
        {
            return;
        }

        var listSettings = listPart.GetSettings<ListPartSettings>();
        var accountSettings = accountPart.GetSettings<AccountPartSettings>();

        var contained = (listSettings.ContainedContentTypes ?? []).ToList();
        var offered = (accountSettings.OfferedContentTypes ?? []).ToHashSet(StringComparer.Ordinal);

        var added = childTypes.Where(type => !offered.Contains(type)).ToArray();
        var removed = contained.Where(excludedTypes.Contains).ToArray();

        if (added.Length == 0 && removed.Length == 0)
        {
            return;
        }

        foreach (var type in added)
        {
            offered.Add(type);

            if (!contained.Contains(type, StringComparer.Ordinal))
            {
                contained.Add(type);
            }
        }

        contained.RemoveAll(excludedTypes.Contains);

        listSettings.ContainedContentTypes = contained.ToArray();
        accountSettings.OfferedContentTypes = offered.Order(StringComparer.Ordinal).ToArray();

        await _contentDefinitionManager.AlterTypeDefinitionAsync(accountType.Name, type => type
            .WithPart(listPart.Name, listPart.PartDefinition.Name, part => part.WithSettings(listSettings))
            .WithPart(accountPart.Name, accountPart.PartDefinition.Name, part => part.WithSettings(accountSettings)));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Updated the list of the '{AccountType}' account type: added {AddedTypes}, removed {RemovedTypes}.",
                accountType.Name,
                string.Join(", ", added),
                string.Join(", ", removed));
        }
    }
}
