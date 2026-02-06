using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Azure.ViewModels;

/// <summary>
/// View model for Azure MongoDB data source configuration.
/// Contains MongoDB-specific connection settings.
/// </summary>
public class AzureMongoDBDataSourceViewModel
{
    public string IndexName { get; set; }

    public string EndpointName { get; set; }

    public string DatabaseName { get; set; }

    public string CollectionName { get; set; }

    public string AppName { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    [BindNever]
    public bool HasPassword { get; set; }
}
