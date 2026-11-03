using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Answers which content types are account types, from the content definitions, and which can belong to an account,
/// from the registered <see cref="IAccountChildTypeRule"/>s.
/// </summary>
public sealed class AccountContentTypeProvider : IAccountContentTypeProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<IAccountChildTypeRule> _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountContentTypeProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="rules">The rules that decide which types can belong to an account.</param>
    public AccountContentTypeProvider(
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IAccountChildTypeRule> rules)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _rules = rules;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContentTypeDefinition>> GetAccountTypesAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(AccountRecordKinds.IsAccount)
            .ToArray();

    /// <inheritdoc/>
    public async Task<bool> IsAccountTypeAsync(string contentType)
        => !string.IsNullOrEmpty(contentType) &&
        AccountRecordKinds.IsAccount(await _contentDefinitionManager.GetTypeDefinitionAsync(contentType));

    /// <inheritdoc/>
    public async Task<bool> IsAccountChildTypeAsync(string contentType)
        => !string.IsNullOrEmpty(contentType) &&
        IsAccountChildType(await _contentDefinitionManager.GetTypeDefinitionAsync(contentType));

    /// <inheritdoc/>
    public bool IsAccountChildType(ContentTypeDefinition definition)
        => definition is not null &&
        !AccountRecordKinds.IsAccount(definition) &&
        !IsExcludedFromAccounts(definition) &&
        _rules.Any(rule => rule.CanBelongToAccount(definition));

    /// <inheritdoc/>
    public bool IsExcludedFromAccounts(ContentTypeDefinition definition)
        => definition is not null && _rules.Any(rule => rule.MustNotBelongToAccount(definition));
}
