using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class EditAIChatProfileViewModel
{
    [Required(AllowEmptyStrings = false)]
    public string Title { get; set; }
}
