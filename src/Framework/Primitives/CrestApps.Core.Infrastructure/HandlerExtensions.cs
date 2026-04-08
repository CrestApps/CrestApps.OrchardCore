using Microsoft.Extensions.Logging;

namespace CrestApps.Core.Infrastructure;

public static class HandlerExtensions
{
    public static async Task InvokeHandlersAsync<THandler, TContext>(
        this IEnumerable<THandler> handlers,
        Func<THandler, TContext, Task> action,
        TContext context,
        ILogger logger)
    {
        foreach (var handler in handlers)
        {
            try
            {
                await action(handler, context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error invoking handler '{HandlerType}'.", handler.GetType().Name);
            }
        }
    }
}
