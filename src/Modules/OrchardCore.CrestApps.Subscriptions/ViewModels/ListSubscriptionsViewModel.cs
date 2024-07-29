using OrchardCore.DisplayManagement;

namespace OrchardCore.CrestApps.Subscriptions.ViewModels;

public class ListSubscriptionsViewModel
{
    public IShape Pager { get; set; }

    public IList<IShape> Subscriptions { get; set; }
}
