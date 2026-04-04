using CrestApps.AI.Models;
using CrestApps.AI.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.AI.ViewModels;

public sealed class AIConnectionViewModel
{
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string DisplayText { get; set; }

    public string Source { get; set; }

    // Provider-specific connection settings.
    public string Endpoint { get; set; }

    public string AuthenticationType { get; set; }

    public string ApiKey { get; set; }

    public bool IsReadOnly { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Providers { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> AuthenticationTypes { get; set; } = [];

    public static AIConnectionViewModel FromConnection(AIProviderConnection connection)
    {
        var model = new AIConnectionViewModel
        {
            ItemId = connection.ItemId,
            Name = connection.Name,
            DisplayText = connection.DisplayText,
            Source = AIProviderNameNormalizer.Normalize(connection.Source),
        };

        // Read provider-specific settings from Properties dictionary.

        if (connection.Properties != null)
        {
            model.Endpoint = connection.Properties.TryGetValue("Endpoint", out var ep) ? ep?.ToString() : null;
            model.ApiKey = connection.Properties.TryGetValue("ApiKey", out var key) ? key?.ToString() : null;
            model.AuthenticationType = connection.Properties.TryGetValue("AuthenticationType", out var auth) ? auth?.ToString() : null;
        }

        return model;
    }

    public static AIConnectionViewModel FromConfiguration(string itemId, string name, string displayText, string source)
        => new()
        {
            ItemId = itemId,
            Name = name,
            DisplayText = displayText,
            Source = AIProviderNameNormalizer.Normalize(source),
            IsReadOnly = true,
        };

    public void ApplyTo(AIProviderConnection connection)
    {
        connection.Name = Name;
        connection.DisplayText = DisplayText;
        connection.Source = AIProviderNameNormalizer.Normalize(Source);

        // Store provider-specific settings in Properties dictionary.
        connection.Properties ??= new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(Endpoint))
        {
            connection.Properties["Endpoint"] = Endpoint;
        }
        else
        {
            connection.Properties.Remove("Endpoint");
        }

        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            connection.Properties["ApiKey"] = ApiKey;
        }

        // Don't remove existing ApiKey when blank (preserve on edit).

        if (!string.IsNullOrWhiteSpace(AuthenticationType))
        {
            connection.Properties["AuthenticationType"] = AuthenticationType;
        }
        else
        {
            connection.Properties.Remove("AuthenticationType");
        }
    }
}
