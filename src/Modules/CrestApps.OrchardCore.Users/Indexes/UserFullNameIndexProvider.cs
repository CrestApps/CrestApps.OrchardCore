using CrestApps.Core.Support;
using CrestApps.OrchardCore.Users.Core.Indexes;
using CrestApps.OrchardCore.Users.Core.Models;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Entities;
using OrchardCore.Users.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Users.Indexes;

/// <summary>
/// Provides user full name index functionality.
/// </summary>
public sealed class UserFullNameIndexProvider : IndexProvider<User>
{
    private readonly ILookupNormalizer _lookupNormalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserFullNameIndexProvider"/> class.
    /// </summary>
    /// <param name="lookupNormalizer">The lookup normalizer.</param>
    public UserFullNameIndexProvider(ILookupNormalizer lookupNormalizer)
    {
        _lookupNormalizer = lookupNormalizer;
    }

    public override void Describe(DescribeContext<User> context)
    {
        context.For<UserFullNameIndex>()
            .Map(user =>
            {
                if (user.TryGet<UserFullNamePart>(out var part))
                {
                    return new UserFullNameIndex()
                    {
                        FirstName = GetLookupValue(part.FirstName),
                        LastName = GetLookupValue(part.LastName),
                        MiddleName = GetLookupValue(part.MiddleName),
                        DisplayName = GetLookupValue(part.DisplayName),
                    };
                }

                return null;
            });
    }

    private string GetLookupValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return _lookupNormalizer.NormalizeName(Str.Truncate(value, 255));
    }
}
