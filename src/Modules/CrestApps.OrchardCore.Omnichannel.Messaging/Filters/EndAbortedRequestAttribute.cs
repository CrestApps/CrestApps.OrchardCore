using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Filters;

/// <summary>
/// Ends a request quietly when its client has gone and a database call it was making failed because of that. SQL
/// Server reports a query cancelled with the request as a "severe error" exception rather than a cancellation, which the
/// exception handler logged as an unhandled error each time an agent moved to another conversation before the page was
/// built. Nobody is waiting for the response.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class EndAbortedRequestAttribute : ExceptionFilterAttribute
{
    /// <inheritdoc/>
    public override void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Exception is DbException && context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            context.ExceptionHandled = true;
            context.Result = new EmptyResult();
        }
    }
}
