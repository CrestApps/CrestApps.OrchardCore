using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
