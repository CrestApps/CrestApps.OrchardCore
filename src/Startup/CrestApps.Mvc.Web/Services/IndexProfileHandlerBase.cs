using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Services;

public abstract class IndexProfileHandlerBase : IIndexProfileHandler
{
    public virtual Task SynchronizedAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
