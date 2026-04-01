using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Services;

public interface IIndexProfileHandler
{
    Task SynchronizedAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default);
}
