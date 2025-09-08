using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.ViewModels;

public sealed class ListOmnichannelActivityContainer : ShapeViewModel
{
    public ListOmnichannelActivityContainer()
        : base("ListOmnichannelActivityContainer")
    {
    }

    [BindNever]
    public IShape Header { get; set; }

    [BindNever]
    public List<IShape> Containers { get; set; }

    [BindNever]
    public IShape Pager { get; set; }
}
