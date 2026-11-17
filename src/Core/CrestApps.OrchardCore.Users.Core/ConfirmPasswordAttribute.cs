using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Asks for the signed-in person's password before the action runs, unless they confirmed it within the last minutes
/// (<see cref="IPasswordConfirmation.Window"/>), then returns to the page. Put it on actions that show something
/// sensitive, such as pay statements. Without the confirmation service the action is refused, never shown unguarded.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class ConfirmPasswordAttribute : Attribute, IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;

        if (httpContext.RequestServices.GetService<IPasswordConfirmation>() is not { } confirmation)
        {
            context.Result = new ForbidResult();

            return;
        }

        if (await confirmation.IsConfirmedAsync(httpContext.User, httpContext.RequestAborted))
        {
            await next();

            return;
        }

        context.Result = new RedirectResult(confirmation.GetConfirmationUrl(ReturnUrl(httpContext.Request)) ?? "/");
    }

    // The page itself for a GET; for a form posted after the confirmation ran out, the page the form was on.
    private static string ReturnUrl(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method))
        {
            return request.PathBase + request.Path + request.QueryString;
        }

        if (Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
            && string.Equals(referer.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return referer.PathAndQuery;
        }

        return request.PathBase + "/";
    }
}
