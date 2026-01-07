using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.SignalR.Filters;

/// <summary>
/// A hub filter that ensures the document store is committed after hub method invocation.
/// Note: This filter works for non-streaming hub methods. For streaming methods (those returning
/// ChannelReader or IAsyncEnumerable), the commit must be handled explicitly in the hub method
/// after the streaming is complete, as the filter cannot wait for the streaming to finish.
/// </summary>
public sealed class SessionHubFilter : IHubFilter
{
    public async ValueTask<object> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object>> next)
    {
        var documentStore = invocationContext.ServiceProvider?.GetService<IDocumentStore>();

        try
        {
            var result = await next(invocationContext);

            // For non-streaming methods, commit the document store after the method completes.
            // For streaming methods (ChannelReader, IAsyncEnumerable), the result is returned immediately
            // and the actual work happens later - those methods must handle commit explicitly.
            if (documentStore is not null)
            {
                await documentStore.CommitAsync();
            }

            return result;
        }
        catch
        {
            // Cancel any pending changes on exception.
            if (documentStore is not null)
            {
                await documentStore.CancelAsync();
            }

            throw;
        }
    }
}
