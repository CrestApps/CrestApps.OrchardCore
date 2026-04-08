using ISession = YesSql.ISession;

namespace CrestApps.Core.Mvc.Web.Services;

internal sealed class YesSqlUnitOfWorkMiddleware
{
    private readonly RequestDelegate _next;

    public YesSqlUnitOfWorkMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISession session)
    {
        await _next(context);
        await session.SaveChangesAsync();
    }
}
