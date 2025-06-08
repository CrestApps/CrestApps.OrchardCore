using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

public sealed class DefaultSubscriptionAdminListFilterProvider : ISubscriptionAdminListFilterProvider
{
    public void Build(QueryEngineBuilder<SubscriptionSession> builder)
    {
        builder
            .WithNamedTerm("status", builder => builder
                .OneCondition((val, query, ctx) =>
                {
                    if (Enum.TryParse<SubscriptionSessionStatus>(val, true, out var status))
                    {
                        query.With<SubscriptionSessionIndex>(x => x.Status == status);
                    }

                    return ValueTask.FromResult(query);
                })
                .MapFrom<ListSubscriptionOptions>((model) =>
                {
                    if (model.Status.HasValue)
                    {
                        return (true, model.Status.ToString());
                    }

                    return (false, string.Empty);
                })
                .AlwaysRun()
             )
            .WithNamedTerm("sort", builder => builder
                .OneCondition((val, query, ctx) =>
                {
                    if (Enum.TryParse<SubscriptionOrder>(val, true, out var sort) && sort == SubscriptionOrder.Oldest)
                    {
                        return ValueTask.FromResult<IQuery<SubscriptionSession>>(query.With<SubscriptionSessionIndex>().OrderBy(x => x.CreatedUtc));
                    }

                    return ValueTask.FromResult<IQuery<SubscriptionSession>>(query.With<SubscriptionSessionIndex>().OrderByDescending(x => x.CreatedUtc));
                })
                .MapTo<ListSubscriptionOptions>((val, model) =>
                {
                    if (Enum.TryParse<SubscriptionOrder>(val, true, out var sort))
                    {
                        model.OrderBy = sort;
                    }
                })
                .MapFrom<ListSubscriptionOptions>((model) =>
                {
                    if (model.OrderBy.HasValue)
                    {
                        return (true, model.OrderBy.ToString());
                    }

                    return (false, string.Empty);
                })
                .AlwaysRun()
            )
            // Always filter by owner to ensure we only query subscriptions that belong to the current user.
            .WithDefaultTerm("owner", builder => builder
                .OneCondition(async (val, query, ctx) =>
                {
                    var context = (SubscriptionQueryContext)ctx;
                    var httpAccessor = context.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                    var authorizationService = context.ServiceProvider.GetRequiredService<IAuthorizationService>();

                    if (await authorizationService.AuthorizeAsync(httpAccessor.HttpContext.User, SubscriptionPermissions.ManageSubscriptions))
                    {
                        return query;
                    }

                    var userId = httpAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

                    return query.With<SubscriptionSessionIndex>(t => t.OwnerId == userId);
                }).AlwaysRun()
            );
    }
}
