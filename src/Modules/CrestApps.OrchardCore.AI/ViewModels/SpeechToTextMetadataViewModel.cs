using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class SpeechToTextMetadataViewModel
{
    public bool UseMicrophone { get; set; }

    public string ConnectionName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Connections { get; set; }
}
