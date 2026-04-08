using Microsoft.Extensions.Logging;

namespace CrestApps.Core.Extensions;

public static class HandlerExtensions
{
    /// <summary>
    /// Invokes a handler delegate on each item in the enumerable, logging and swallowing
    /// any exceptions thrown by individual handlers.
    /// This is the Framework-level equivalent of OrchardCore.Modules.InvokeAsync.
    /// </summary>
    public static async Task InvokeAsync<THandler, TContext>(
        this IEnumerable<THandler> handlers,
        Func<THandler, TContext, Task> dispatch,
        TContext context,
        ILogger logger)
    {
        foreach (var handler in handlers)
        {
            try
            {
                await dispatch(handler, context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing the handler '{HandlerType}'.", handler.GetType().Name);
            }
        }
    }
}
