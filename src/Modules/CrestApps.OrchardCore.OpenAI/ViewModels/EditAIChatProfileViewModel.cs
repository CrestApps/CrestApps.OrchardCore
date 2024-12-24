using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class EditAIChatProfileViewModel
{
    public string Name { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
