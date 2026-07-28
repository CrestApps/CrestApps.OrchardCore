using CrestApps.Core.Models;
using CrestApps.Core.Services;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Core.Validation;

/// <summary>
/// Surfaces the rules a catalog entry is validated by, wherever an editor is the thing doing the writing.
/// </summary>
/// <remarks>
/// A rule that decides whether a record may be stored belongs to the record, not to one of the ways a record can be
/// edited. Keeping the rules in the entry's handlers means the editor, a recipe, a deployment plan and any service
/// that writes through the manager all enforce the same set, and an editor is left with the job it actually owns:
/// binding what the operator typed and reporting what the rules said about it.
/// </remarks>
public static class CatalogEntryValidation
{
    /// <summary>
    /// Validates an entry through the rules its handlers declare and reports every failure against the editor.
    /// </summary>
    /// <typeparam name="T">The catalog entry type being validated.</typeparam>
    /// <param name="manager">The manager that owns the entry type.</param>
    /// <param name="entry">The entry as the editor has just bound it.</param>
    /// <param name="updater">The editor to report failures against.</param>
    /// <param name="prefix">The editor's model prefix.</param>
    /// <param name="cancellationToken">A token that cancels the validation.</param>
    /// <returns><see langword="true"/> when the entry satisfies every rule; otherwise, <see langword="false"/>.</returns>
    public static async Task<bool> ValidateAsync<T>(
        ICatalogManager<T> manager,
        T entry,
        IUpdateModel updater,
        string prefix,
        CancellationToken cancellationToken = default)
        where T : CatalogItem, new()
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(updater);

        var result = await manager.ValidateAsync(entry, cancellationToken);

        if (result.Succeeded)
        {
            return true;
        }

        foreach (var error in result.Errors)
        {
            var members = error.MemberNames?.Where(member => !string.IsNullOrEmpty(member)).ToArray();

            if (members is null || members.Length == 0)
            {
                updater.ModelState.AddModelError(GetKey(prefix, null), error.ErrorMessage);

                continue;
            }

            foreach (var member in members)
            {
                updater.ModelState.AddModelError(GetKey(prefix, member), error.ErrorMessage);
            }
        }

        return false;
    }

    private static string GetKey(string prefix, string member)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return member ?? string.Empty;
        }

        return string.IsNullOrEmpty(member)
            ? prefix
            : $"{prefix}.{member}";
    }
}
