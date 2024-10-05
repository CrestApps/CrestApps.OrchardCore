using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class ListSubscriptionsViewModel
{
    public IShape Pager { get; set; }

    public IList<IShape> Subscriptions { get; set; }
}
