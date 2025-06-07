using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Azure.ViewModels;

public class AzureProfileMongoDBViewModel
{
    public string IndexName { get; set; }

    public string EndpointName { get; set; }

    public string CollectionName { get; set; }

    public string AppName { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    [Range(1, 5)]
    public int? Strictness { get; set; }

    [Range(3, 20)]
    public int? TopNDocuments { get; set; }

    [BindNever]
    public bool HasPassword { get; set; }
}
