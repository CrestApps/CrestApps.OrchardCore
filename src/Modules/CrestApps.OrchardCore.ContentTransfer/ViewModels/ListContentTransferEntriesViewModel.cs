using Microsoft.AspNetCore.Mvc.ModelBinding;
using CrestApps.OrchardCore.ContentTransfer.Models;

namespace CrestApps.OrchardCore.ContentTransfer.ViewModels;

public class ListContentTransferEntriesViewModel
{
    public ListContentTransferEntryOptions Options { get; set; }

    [BindNever]
    public IList<dynamic> Entries { get; set; }

    [BindNever]
    public dynamic Header { get; set; }

    [BindNever]
    public dynamic Pager { get; set; }
}
