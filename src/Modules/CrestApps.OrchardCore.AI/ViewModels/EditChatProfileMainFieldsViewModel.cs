using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditChatProfileMainFieldsViewModel
{
    public string Name { get; set; }

    public string DisplayText { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
