using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureAIChatProfileAISearchMetadata
{
    [Required(AllowEmptyStrings = false)]
    public string IndexName { get; set; }
}
