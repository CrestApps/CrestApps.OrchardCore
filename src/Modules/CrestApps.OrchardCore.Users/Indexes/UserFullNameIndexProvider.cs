using CrestApps.OrchardCore.Users.Core.Indexes;
using CrestApps.OrchardCore.Users.Models;
using CrestApps.Support;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Entities;
using OrchardCore.Users.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Users.Indexes;

public sealed class UserFullNameIndexProvider : IndexProvider<User>
{
    private readonly ILookupNormalizer _lookupNormalizer;

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
