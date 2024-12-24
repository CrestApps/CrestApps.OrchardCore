using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureAIChatProfileSearchAIMetadata
{
    [Required(AllowEmptyStrings = false)]
    public string IndexName { get; set; }
}
