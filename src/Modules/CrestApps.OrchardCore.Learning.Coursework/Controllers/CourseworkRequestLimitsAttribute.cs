using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Learning.Coursework.Controllers;

/// <summary>
/// Lets a hand-in request be as large as the largest hand-in <see cref="CourseworkOptions"/> allows (every file at the
/// largest size), instead of the server's default request limit. It runs before the form is read, as
/// <c>[RequestSizeLimit]</c> and <c>[RequestFormLimits]</c> do, with a limit taken from configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class CourseworkRequestLimitsAttribute : Attribute, IAuthorizationFilter, IOrderedFilter
{
    /// <inheritdoc />
    public int Order => 900;

    /// <inheritdoc />
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        var limit = httpContext.RequestServices.GetRequiredService<IOptions<CourseworkOptions>>().Value.MaxRequestBytes;
        var bodySize = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (bodySize is { IsReadOnly: false })
        {
            bodySize.MaxRequestBodySize = limit;
        }

        if (httpContext.Request.HasFormContentType && httpContext.Features.Get<IFormFeature>() is not { Form: not null })
        {
            httpContext.Features.Set<IFormFeature>(new FormFeature(httpContext.Request, new FormOptions
            {
                MultipartBodyLengthLimit = limit,
            }));
        }
    }
}
