using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Answers which content types are account types and which can belong to an account.
/// </summary>
public interface IAccountContentTypeProvider
{
    /// <summary>
    /// Gets the definitions of the account types.
    /// </summary>
    Task<IReadOnlyList<ContentTypeDefinition>> GetAccountTypesAsync();

    /// <summary>
    /// Determines whether the content type is an account type.
    /// </summary>
    /// <param name="contentType">The technical name of the content type.</param>
    Task<bool> IsAccountTypeAsync(string contentType);

    /// <summary>
    /// Determines whether items of the content type can belong to an account.
    /// </summary>
    /// <param name="contentType">The technical name of the content type.</param>
    Task<bool> IsAccountChildTypeAsync(string contentType);

    /// <summary>
    /// Determines whether items of the content type can belong to an account.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    bool IsAccountChildType(ContentTypeDefinition definition);

    /// <summary>
    /// Determines whether items of the content type must never belong to an account.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    bool IsExcludedFromAccounts(ContentTypeDefinition definition);
}
