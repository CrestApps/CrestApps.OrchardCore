using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Tells account types apart by the part they carry. A type is never named in code: an account is any type with
/// <c>AccountPart</c>.
/// </summary>
public static class AccountRecordKinds
{
    /// <summary>
    /// Determines whether the content type is an account type.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    public static bool IsAccount(ContentTypeDefinition definition)
        => HasPart(definition, AccountsConstants.ContentParts.Account);

    /// <summary>
    /// Determines whether the content type carries the named part.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    /// <param name="partName">The technical name of the part definition.</param>
    public static bool HasPart(ContentTypeDefinition definition, string partName)
        => ContentTypeDefinitionPartExtensions.HasPart(definition, partName);
}
