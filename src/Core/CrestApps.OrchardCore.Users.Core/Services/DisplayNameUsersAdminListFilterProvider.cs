using CrestApps.OrchardCore.Users.Core.Indexes;
using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using YesSql;
using YesSql.Filters.Query;
using YesSql.Services;

namespace CrestApps.OrchardCore.Users.Core.Services;

public sealed class DisplayNameUsersAdminListFilterProvider : IUsersAdminListFilterProvider
{
    public const string DefaultTermName = "display-name";

    public void Build(QueryEngineBuilder<User> builder)
    {
        builder
            .WithNamedTerm(DefaultTermName, builder => builder
                .ManyCondition(
                    async (val, query, ctx) =>
                    {
                        var context = (UserQueryContext)ctx;
                        var userManager = context.ServiceProvider.GetRequiredService<UserManager<IUser>>();
                        var siteService = context.ServiceProvider.GetRequiredService<ISiteService>();
                        var normalizer = context.ServiceProvider.GetRequiredService<ILookupNormalizer>();
                        var settings = (await siteService.GetSiteSettingsAsync()).As<DisplayNameSettings>();
                        var normalizedUserName = userManager.NormalizeName(val);

                        var predicates = new List<Func<IQuery<User>, IQuery<User>>>()
                        {
                            q => q.With<UserIndex>(i => i.NormalizedUserName != null && i.NormalizedUserName.Contains(normalizedUserName)),
                        };

                        var normalizedValue = normalizer.NormalizeName(val);

                        if (settings.DisplayName != DisplayNamePropertyType.None)
                        {
                            predicates.Add(q => q.With<UserFullNameIndex>(i => i.DisplayName != null && i.DisplayName.Contains(normalizedValue)));
                        }

                        if (settings.FirstName != DisplayNamePropertyType.None)
                        {
                            predicates.Add(q => q.With<UserFullNameIndex>(i => i.FirstName != null && i.FirstName.Contains(normalizedValue)));
                        }

                        if (settings.LastName != DisplayNamePropertyType.None)
                        {
                            predicates.Add(q => q.With<UserFullNameIndex>(i => i.LastName != null && i.LastName.Contains(normalizedValue)));
                        }

                        if (settings.MiddleName != DisplayNamePropertyType.None)
                        {
                            predicates.Add(q => q.With<UserFullNameIndex>(i => i.MiddleName != null && i.MiddleName.Contains(normalizedValue)));
                        }

                        return query.Any(predicates.ToArray());
                    },
                    async (val, query, ctx) =>
                    {
                        var context = (UserQueryContext)ctx;
                        var userManager = context.ServiceProvider.GetRequiredService<UserManager<IUser>>();
                        var normalizedUserName = userManager.NormalizeName(val);
                        var siteService = context.ServiceProvider.GetRequiredService<ISiteService>();
                        var normalizer = context.ServiceProvider.GetRequiredService<ILookupNormalizer>();
                        var settings = (await siteService.GetSiteSettingsAsync()).As<DisplayNameSettings>();

                        var predicates = new List<Func<IQuery<User>, IQuery<User>>>()
                        {
                            q => q.With<UserIndex>(i => i.NormalizedUserName == null || i.NormalizedUserName.NotContains(normalizedUserName)),
                        };

                        var normalizedValue = normalizer.NormalizeName(val);

                        if (settings.DisplayName != DisplayNamePropertyType.None)
                        {
                            predicates.Add(q => q.With<UserFullNameIndex>(i => i.DisplayName == null || i.DisplayName.NotContains(normalizedValue)));
                        }

                        if (settings.FirstName != DisplayNamePropertyType.None)
                        {
                            predicates.Add(q => q.With<UserFullNameIndex>(i => i.FirstName == null || i.FirstName.NotContains(normalizedValue)));
                        }

                        if (settings.LastName != DisplayNamePropertyType.None)
                        {
                            predicates.Add(q => q.With<UserFullNameIndex>(i => i.LastName == null || i.LastName.NotContains(normalizedValue)));
                        }

                        if (settings.MiddleName != DisplayNamePropertyType.None)
                        {
                            predicates.Add(q => q.With<UserFullNameIndex>(i => i.MiddleName == null || i.MiddleName.NotContains(normalizedValue)));
                        }

                        return query.All(predicates.ToArray());
                    }
                )
            );
    }
}
