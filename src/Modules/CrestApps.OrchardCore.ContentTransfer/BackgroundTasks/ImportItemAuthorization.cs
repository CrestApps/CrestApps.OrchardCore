using System.Security.Claims;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Entities;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

/// <summary>
/// Checks, when <see cref="ContentImportOptions.RequireEditPermissionPerItem"/> is on, that the uploader of an import may
/// change each existing item a row names, as the uploader's own request would be checked.
/// </summary>
internal static class ImportItemAuthorization
{
    /// <summary>
    /// Builds the check for an import.
    /// </summary>
    /// <param name="services">The services of the scope.</param>
    /// <param name="entry">The import.</param>
    /// <param name="options">The import options.</param>
    /// <returns>
    /// The check, or <see langword="null"/> when items aren't checked one by one. When the uploader no longer exists,
    /// every existing item is refused.
    /// </returns>
    public static async Task<Func<ContentItem, Task<bool>>> CreateAsync(IServiceProvider services, ContentTransferEntry entry, ContentImportOptions options)
    {
        if (options?.RequireEditPermissionPerItem != true)
        {
            return null;
        }

        var principal = await CreatePrincipalAsync(services, entry.Owner);

        if (principal is null)
        {
            return _ => Task.FromResult(false);
        }

        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var publishes = entry.GetOrCreate<ImportContentOptionsPart>().PublishImportedContent;

        return async contentItem =>
            await authorizationService.AuthorizeAsync(principal, CommonPermissions.EditContent, contentItem) &&
            (!publishes || await authorizationService.AuthorizeAsync(principal, CommonPermissions.PublishContent, contentItem));
    }

    private static async Task<ClaimsPrincipal> CreatePrincipalAsync(IServiceProvider services, string userId)
    {
        if (string.IsNullOrEmpty(userId) ||
            services.GetService<UserManager<IUser>>() is not { } users ||
            services.GetService<IUserClaimsPrincipalFactory<IUser>>() is not { } principals ||
            await users.FindByIdAsync(userId) is not { } user)
        {
            return null;
        }

        return await principals.CreateAsync(user);
    }
}
