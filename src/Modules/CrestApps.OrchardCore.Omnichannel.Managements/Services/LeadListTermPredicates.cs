using System.Collections.Frozen;
using System.Linq.Expressions;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Builds the lead index predicate of each lead term of the content list search box: <c>lead-status:</c>,
/// <c>converted:</c>, <c>source:</c>, <c>list:</c>, <c>owner:</c> and <c>rating:</c>.
/// </summary>
/// <remarks>
/// The content list filters by these predicates, and <see cref="LeadListCountQueryFactory"/> counts the leads of the
/// list with the same ones, so the count always agrees with the rows the list shows.
/// </remarks>
internal static class LeadListTermPredicates
{
    public const string LeadStatusTermName = "lead-status";

    public const string ConvertedTermName = "converted";

    public const string SourceTermName = "source";

    public const string ListTermName = "list";

    public const string OwnerTermName = "owner";

    public const string RatingTermName = "rating";

    /// <summary>
    /// The names of the lead terms.
    /// </summary>
    public static readonly FrozenSet<string> TermNames = new[]
    {
        LeadStatusTermName,
        ConvertedTermName,
        SourceTermName,
        ListTermName,
        OwnerTermName,
        RatingTermName,
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Builds the lead index predicate of a lead term.
    /// </summary>
    /// <param name="termName">The name of the term, one of <see cref="TermNames"/>.</param>
    /// <param name="value">The value typed after the term name.</param>
    /// <param name="serviceProvider">The services of the request.</param>
    /// <returns>The predicate over the lead index.</returns>
    public static async ValueTask<Expression<Func<LeadIndex, bool>>> BuildAsync(
        string termName,
        string value,
        IServiceProvider serviceProvider)
    {
        ArgumentException.ThrowIfNullOrEmpty(termName);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (IsTerm(termName, LeadStatusTermName))
        {
            var statusId = await ResolveCatalogIdAsync<LeadStatus>(value, serviceProvider);

            return index => index.StatusId == statusId;
        }

        if (IsTerm(termName, ConvertedTermName))
        {
            var converted = ParseBoolean(value);

            return index => index.IsConverted == converted;
        }

        if (IsTerm(termName, SourceTermName))
        {
            var sourceId = await ResolveLeadSourceIdAsync(value, serviceProvider);

            return index => index.SourceId == sourceId;
        }

        if (IsTerm(termName, ListTermName))
        {
            return index => index.ListName == value;
        }

        if (IsTerm(termName, OwnerTermName))
        {
            var ownerId = await ResolveUserIdAsync(value, serviceProvider);

            return index => index.OwnerId == ownerId;
        }

        if (IsTerm(termName, RatingTermName))
        {
            var ratings = serviceProvider.GetRequiredService<LeadRatingProvider>();
            var rating = await ratings.NormalizeAsync(value) ?? value;

            return index => index.Rating == rating;
        }

        throw new ArgumentOutOfRangeException(nameof(termName), termName, "The term is not a lead term.");
    }

    /// <summary>
    /// Reads a yes/no term value.
    /// </summary>
    /// <param name="value">The value typed after the term name.</param>
    /// <returns><see langword="true"/> for <c>true</c>, <c>yes</c>, <c>1</c> or <c>y</c>; otherwise, <see langword="false"/>.</returns>
    public static bool ParseBoolean(string value)
        => value?.Trim().ToLowerInvariant() is "true" or "yes" or "1" or "y";

    /// <summary>
    /// Resolves the identifier of a catalog entry, such as a lead status or an opportunity stage, from the name a term
    /// gives it. An identifier works too, for links built by code.
    /// </summary>
    /// <typeparam name="T">The catalog entry type.</typeparam>
    /// <param name="value">The name or identifier typed after the term name.</param>
    /// <param name="serviceProvider">The services of the request.</param>
    /// <returns>The identifier of the entry, or <paramref name="value"/> when no entry has that name.</returns>
    public static async ValueTask<string> ResolveCatalogIdAsync<T>(string value, IServiceProvider serviceProvider)
        where T : CrestApps.Core.Models.CatalogItem, CrestApps.Core.INameAwareModel
    {
        var catalog = serviceProvider.GetRequiredService<INamedCatalog<T>>();

        var entry = await catalog.FindByNameAsync(value);

        return entry?.ItemId ?? value;
    }

    private static bool IsTerm(string termName, string expected)
        => string.Equals(termName, expected, StringComparison.OrdinalIgnoreCase);

    // People type the name of a lead source; the index stores the id of its content item. An id works too.
    private static async ValueTask<string> ResolveLeadSourceIdAsync(string value, IServiceProvider serviceProvider)
    {
        var sources = serviceProvider.GetRequiredService<LeadSourceProvider>();

        return await sources.FindIdAsync(value) ?? value;
    }

    // People type a user name; the index stores the user id. An id works too, for links built by code.
    private static async ValueTask<string> ResolveUserIdAsync(string value, IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<IUser>>();

        return await userManager.FindByNameAsync(value) is User user ? user.UserId : value;
    }
}
