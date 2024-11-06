using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class ListServicePlansViewModel
{
    public IShape Pager { get; set; }

    public IList<IShape> ServicePlans { get; set; }
}
