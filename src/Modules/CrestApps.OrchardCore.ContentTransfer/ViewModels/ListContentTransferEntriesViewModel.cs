using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.ContentTransfer.ViewModels;

public class ListContentTransferEntriesViewModel
{
    public ListContentTransferEntryOptions Options { get; set; }

    [BindNever]
    public AdminListContext List { get; set; }

    [BindNever]
    public dynamic Header { get; set; }

    [BindNever]
    public dynamic Pager { get; set; }
}
